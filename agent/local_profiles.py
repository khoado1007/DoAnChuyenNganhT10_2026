"""Vùng file mô phỏng; nội dung chỉ nằm tại Agent, ngoài Windows profile thật."""
from __future__ import annotations

import base64
import binascii
from datetime import datetime, timezone
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import tempfile
import threading
import uuid

MAX_BYTES = 8 * 1024 * 1024
ALLOWED = {'.txt', '.csv', '.doc', '.docx', '.xls', '.xlsx', '.pdf', '.ppt', '.pptx', '.ini'}


class FileError(Exception):
    pass


def safe_name(value, nested=False):
    if not isinstance(value, str) or not value or len(value) > 240:
        raise FileError('INVALID_RELATIVE_PATH')
    parts = value.split('/') if nested else [value]
    for part in parts:
        if (not part or part in ('.', '..') or part[-1] in '. '
                or re.search(r'[<>:"/\\|?*\x00-\x1f]', part)
                or re.fullmatch(r'(?i)(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?', part)):
            raise FileError('INVALID_RELATIVE_PATH')
    return value


def checked(path):
    # Chặn reparse point và hardlink trước khi truy cập dữ liệu local.
    for item in [path, *path.parents]:
        if item.exists() or item.is_symlink():
            info = item.lstat()
            if (stat.S_ISLNK(info.st_mode) or getattr(info, 'st_file_attributes', 0) & 0x400
                    or (stat.S_ISREG(info.st_mode) and info.st_nlink > 1)):
                raise FileError('INVALID_RELATIVE_PATH')
    return path


def save_json(path, data):
    from itam_agent import save_private
    checked(path)
    save_private(path, data)


def read_bounded(path):
    with checked(path).open('rb') as stream:
        data = stream.read(MAX_BYTES + 1)
    if len(data) > MAX_BYTES:
        raise FileError('FILE_TOO_LARGE')
    return data


class LocalProfiles:
    def __init__(self, root, mode):
        if mode not in ('real', 'simulated'):
            raise ValueError('Invalid mode')
        self.root = checked(Path(root).absolute()) / mode
        self.lock = threading.Lock()
        self._private_systems = set()

    def execute(self, command):
        with self.lock:
            try:
                return self._execute(command)
            except FileError as error:
                return dict(success=False, errorCode=str(error), files=[], contentBase64=None)
            except (ValueError, TypeError, KeyError, binascii.Error):
                return dict(success=False, errorCode='INVALID_FILE_OPERATION', files=[], contentBase64=None)
            except OSError:
                return dict(success=False, errorCode='FILE_OPERATION_UNKNOWN', files=[], contentBase64=None)

    def _execute(self, c):
        ids = {key: str(uuid.UUID(c[key])) for key in ('deviceId', 'userId', 'profileId', 'operationId')}
        device = checked(self.root / 'Devices' / ids['deviceId'])
        profile = checked(device / 'Profiles' / ids['userId'])
        system = checked(device / 'System' / ids['userId'])
        system.mkdir(parents=True, mode=0o700, exist_ok=True)
        if system not in self._private_systems:
            if os.name == 'nt':
                try:
                    from itam_agent import current_windows_account
                    subprocess.run(['icacls', str(system), '/inheritance:r', '/grant:r',
                                    f'{current_windows_account()}:(OI)(CI)(F)'], check=True,
                                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                   creationflags=subprocess.CREATE_NO_WINDOW)
                except (OSError, subprocess.CalledProcessError):
                    raise FileError('LOCAL_STORAGE_PERMISSION_FAILED') from None
            else:
                os.chmod(system, 0o700)
            self._private_systems.add(system)
        journal = checked(system / (ids['operationId'] + '.json'))
        if journal.exists():
            previous = json.loads(journal.read_text(encoding='utf-8'))
            if previous.get('result') is not None:
                return previous['result']
            raise FileError('FILE_OPERATION_UNKNOWN')
        if c.get('recoveryOnly'):
            raise FileError('FILE_OPERATION_UNKNOWN')
        if datetime.fromisoformat(c['expiresAt'].replace('Z', '+00:00')) <= datetime.now(timezone.utc):
            raise FileError('FILE_OPERATION_EXPIRED')
        kind = c['kind']
        if kind not in ('ensure', 'list', 'create', 'read', 'write', 'rename', 'move', 'delete', 'copy', 'restore', 'mkdir'):
            raise FileError('INVALID_FILE_OPERATION')
        index_path = checked(system / 'index.json')
        index = json.loads(index_path.read_text(encoding='utf-8')) if index_path.exists() else dict(profileId=ids['profileId'], live={}, trash={})
        if index['profileId'] != ids['profileId']:
            raise FileError('PROFILE_BINDING_MISMATCH')
        for folder in ('Desktop', 'Documents'):
            checked(profile / folder).mkdir(parents=True, exist_ok=True)
        inventory = self._inventory(profile, index)
        limit = c['maxFiles']
        if type(limit) is not int or limit < 0:
            raise FileError('INVALID_FILE_OPERATION')
        if kind in ('create', 'copy', 'restore') and len(inventory) >= limit:
            raise FileError('FILE_COUNT_QUOTA_EXCEEDED')
        content = None
        if kind not in ('ensure', 'list'):
            file_id = None
            if kind == 'restore':
                file_id = str(uuid.UUID(c['fileId']))
                old_key = index['trash'].get(file_id)
                if old_key is None:
                    raise FileError('FILE_NOT_FOUND')
                old_parts = PurePosixPath(safe_name(old_key, nested=True)).parts
                if len(old_parts) < 2 or old_parts[0] not in ('Desktop', 'Documents'):
                    raise FileError('INVALID_RELATIVE_PATH')
                folder = c.get('targetFolder') or old_parts[0]
                if folder not in ('Desktop', 'Documents'):
                    raise FileError('INVALID_RELATIVE_PATH')
                name = safe_name(c.get('newName') or old_parts[-1])
                key = old_key
                source = checked(system / 'Recycle' / file_id)
            elif kind in ('read', 'write', 'rename', 'move', 'copy', 'delete') and c.get('fileId'):
                file_id = str(uuid.UUID(c['fileId']))
                matches = [path for path, identity in index['live'].items() if identity == file_id]
                if len(matches) != 1:
                    raise FileError('FILE_NOT_FOUND')
                key = safe_name(matches[0], nested=True)
                parts = PurePosixPath(key).parts
                if len(parts) < 2 or parts[0] not in ('Desktop', 'Documents'):
                    raise FileError('INVALID_RELATIVE_PATH')
                folder, name = parts[0], '/'.join(parts[1:])
                source = checked(profile.joinpath(folder, *parts[1:]))
                if c.get('name') and safe_name(c['name'], nested=True) != name:
                    raise FileError('FILE_OPERATION_CONFLICT')
                if c.get('folder') and c['folder'] != folder:
                    raise FileError('FILE_OPERATION_CONFLICT')
            else:
                folder = c.get('folder')
                if folder not in ('Desktop', 'Documents'):
                    raise FileError('INVALID_RELATIVE_PATH')
                name = safe_name(c.get('name'), nested=kind not in ('create', 'mkdir'))
                key = folder + '/' + name
                source = checked(profile.joinpath(folder, *PurePosixPath(name).parts))

            if kind not in ('create', 'mkdir', 'restore') and (
                    not source.is_file() or (kind != 'restore' and key not in index['live'])):
                raise FileError('FILE_NOT_FOUND')
            if c.get('fileId') and kind not in ('create', 'mkdir', 'restore') and index['live'][key] != file_id:
                raise FileError('FILE_OPERATION_CONFLICT')
            if kind == 'read':
                if source.stat().st_size > MAX_BYTES:
                    raise FileError('FILE_TOO_LARGE')
                content = base64.b64encode(read_bounded(source)).decode('ascii')
            else:
                destination = source
                if kind == 'restore':
                    destination = checked(profile / folder / name)
                elif kind in ('rename', 'copy'):
                    new_name = safe_name(c.get('newName'))
                    if kind == 'copy' and c.get('targetFolder'):
                        target_folder = c['targetFolder']
                        if target_folder not in ('Desktop', 'Documents'):
                            raise FileError('INVALID_RELATIVE_PATH')
                        destination = checked(profile / target_folder / new_name)
                    else:
                        destination = checked(source.parent / new_name)
                if kind == 'move':
                    target_folder = c.get('targetFolder')
                    if target_folder not in ('Desktop', 'Documents'):
                        raise FileError('INVALID_RELATIVE_PATH')
                    destination = checked(profile / target_folder / source.name)
                if kind in ('create', 'write', 'rename', 'move', 'copy', 'restore') and destination.suffix.lower() not in ALLOWED:
                    raise FileError('FILE_TYPE_NOT_ALLOWED')
                if kind not in ('write', 'delete'):
                    conflicts = [
                        path for path in destination.parent.iterdir()
                        if path.name.casefold() == destination.name.casefold()
                        and not (kind == 'rename' and path == source)
                    ]
                    if conflicts or (kind == 'copy' and destination == source):
                        raise FileError('FILE_OPERATION_CONFLICT')
                payload = None
                if kind in ('create', 'write'):
                    encoded = c.get('contentBase64') or ''
                    if len(encoded) > ((MAX_BYTES + 2) // 3) * 4:
                        raise FileError('FILE_TOO_LARGE')
                    payload = base64.b64decode(encoded, validate=True)
                    if len(payload) > MAX_BYTES:
                        raise FileError('FILE_TOO_LARGE')
                    if kind == 'write' and source.suffix.lower() not in ('.txt', '.csv'):
                        raise FileError('FILE_TYPE_NOT_EDITABLE')
                if kind == 'copy':
                    if source.stat().st_size > MAX_BYTES:
                        raise FileError('FILE_TOO_LARGE')
                    payload = read_bounded(source)
                if shutil.disk_usage(system).free < (len(payload) if payload else 0) + 16 * 1024 * 1024:
                    raise FileError('DISK_SPACE_LOW')
                # Ghi intent trước mutation; crash không tự thực thi lại thao tác chưa rõ kết quả.
                save_json(journal, dict(result=None))
                if kind in ('create', 'write', 'copy'):
                    self._write(system, destination, payload, overwrite=kind == 'write')
                    dest_key = destination.relative_to(profile).as_posix()
                    index['live'][dest_key] = (
                        str(uuid.uuid4()) if kind == 'copy' else index['live'].get(dest_key, str(uuid.uuid4()))
                    )
                elif kind == 'mkdir':
                    destination.mkdir()
                elif kind in ('move', 'rename'):
                    source.rename(destination)
                    index['live'][destination.relative_to(profile).as_posix()] = index['live'].pop(key)
                elif kind == 'delete':
                    file_id = index['live'][key]
                    recycle = checked(system / 'Recycle')
                    recycle.mkdir(exist_ok=True)
                    source.rename(checked(recycle / file_id))
                    index['trash'][file_id] = key
                    del index['live'][key]
                elif kind == 'restore':
                    source.rename(destination)
                    index['live'][key] = file_id
                    del index['trash'][file_id]
        if kind == 'list' and c.get('folder') not in ('Desktop', 'Documents', 'RecycleBin'):
            raise FileError('INVALID_RELATIVE_PATH')
        inventory = self._inventory(profile, index)
        save_json(index_path, index)
        inventory.extend(self._trash_inventory(system, index))
        result = dict(success=True, errorCode=None, files=inventory, contentBase64=content)
        # Read body không lưu journal; restart báo unknown thay vì đọc lại dữ liệu đã đổi.
        save_json(journal, dict(result=result if content is None else None))
        return result

    @staticmethod
    def _write(system, destination, payload, overwrite=False):
        descriptor, name = tempfile.mkstemp(dir=system, prefix='staging-')
        try:
            with os.fdopen(descriptor, 'wb') as stream:
                stream.write(payload)
                stream.flush()
                os.fsync(stream.fileno())
            checked(destination)
            if overwrite:
                os.replace(name, destination)
            else:
                try:
                    os.link(name, destination)
                except FileExistsError:
                    raise FileError('FILE_OPERATION_CONFLICT') from None
                os.unlink(name)
        finally:
            if os.path.exists(name):
                os.unlink(name)

    @staticmethod
    def _inventory(profile, index):
        files = []
        live = {}
        visited = 0
        for folder in ('Desktop', 'Documents'):
            for directory, dirs, names in os.walk(checked(profile / folder), followlinks=False):
                visited += 1 + len(dirs) + len(names)
                if visited > 10000:
                    raise FileError('PROFILE_SCAN_LIMIT')
                for name in dirs + names:
                    checked(Path(directory) / name)
                for name in names:
                    path = checked(Path(directory) / name)
                    if not path.is_file():
                        raise FileError('INVALID_RELATIVE_PATH')
                    key = path.relative_to(profile).as_posix()
                    file_id = index['live'].get(key, str(uuid.uuid4()))
                    live[key] = file_id
                    files.append(dict(fileId=file_id, name=path.relative_to(profile / folder).as_posix(),
                                      folder=folder, sizeBytes=path.stat().st_size, extension=path.suffix.lower()))
        index['live'] = live
        return sorted(files, key=lambda f: (f['folder'], f['name']))

    @staticmethod
    def _trash_inventory(system, index):
        files = []
        recycle = checked(system / 'Recycle')
        for file_id, old_key in index['trash'].items():
            file_id = str(uuid.UUID(file_id))
            old_path = PurePosixPath(safe_name(old_key, nested=True))
            path = checked(recycle / file_id)
            if not path.is_file():
                raise FileError('FILE_OPERATION_UNKNOWN')
            name = old_path.name
            files.append(dict(fileId=file_id, name=name, folder='RecycleBin',
                              sizeBytes=path.stat().st_size, extension=Path(name).suffix.lower()))
        return sorted(files, key=lambda item: item['name'])
