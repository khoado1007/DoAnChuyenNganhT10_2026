import base64
from datetime import datetime, timedelta, timezone
from pathlib import Path
import sys
import uuid

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from local_profiles import FileError, LocalProfiles


def command(**values):
    defaults = dict(operationId=str(uuid.uuid4()), deviceId=DEVICE, userId=USER,
                    profileId=PROFILE, kind='ensure', folder='Desktop', name='note.txt',
                    maxFiles=10, expiresAt=(datetime.now(timezone.utc) + timedelta(minutes=5)).isoformat())
    return dict(defaults, **values)


DEVICE, USER, PROFILE = (str(uuid.uuid4()) for _ in range(3))


def test_persistence_isolation_and_full_quota(tmp_path):
    engine = LocalProfiles(tmp_path, 'simulated')
    assert engine.execute(command())['files'] == []
    for index in range(10):
        assert engine.execute(command(kind='create', name=f'{index}.txt'))['success']
    assert engine.execute(command(kind='create'))['errorCode'] == 'FILE_COUNT_QUOTA_EXCEEDED'
    assert len(LocalProfiles(tmp_path, 'simulated').execute(command(kind='list'))['files']) == 10
    assert engine.execute(command(userId=str(uuid.uuid4()), profileId=str(uuid.uuid4())))['files'] == []
    assert LocalProfiles(tmp_path, 'real').execute(command())['files'] == []


def test_stable_identity_recycle_and_replay(tmp_path):
    engine = LocalProfiles(tmp_path, 'real')
    create = command(kind='create', contentBase64=base64.b64encode(b'private body').decode())
    first = engine.execute(create)
    assert first['success']
    assert engine.execute(create) == first
    renamed = engine.execute(command(kind='rename', newName='renamed.txt'))
    assert renamed['files'][0]['fileId'] == first['files'][0]['fileId']
    deleted = engine.execute(command(kind='delete', name='renamed.txt'))
    assert not any(file['folder'] in ('Desktop', 'Documents') for file in deleted['files'])
    assert any(file['folder'] == 'RecycleBin' for file in deleted['files'])
    file_id = first['files'][0]['fileId']
    assert engine.execute(command(kind='restore', fileId=file_id, name=None, folder=None, maxFiles=0))['errorCode'] == 'FILE_COUNT_QUOTA_EXCEEDED'
    assert engine.execute(command(kind='restore', fileId=file_id, name=None, folder=None))['success']
    read = engine.execute(command(kind='read', name='renamed.txt'))
    assert base64.b64decode(read['contentBase64']) == b'private body'
    for path in tmp_path.rglob('*.json'):
        assert 'cHJpdmF0ZSBib2R5' not in path.read_text()


@pytest.mark.parametrize('name', ['../bad.txt', 'C:\\bad.txt', 'a:b.txt', 'CON.txt', 'bad.exe', 'bad.txt.', 'a/b.txt'])
def test_rejects_dangerous_names(tmp_path, name):
    assert not LocalProfiles(tmp_path, 'real').execute(command(kind='create', name=name))['success']


def test_counts_nested_and_system_like_files(tmp_path):
    engine = LocalProfiles(tmp_path, 'real')
    engine.execute(command())
    desktop = tmp_path / 'real' / 'Devices' / DEVICE / 'Profiles' / USER / 'Desktop'
    (desktop / 'nested').mkdir()
    (desktop / 'nested' / 'desktop.ini').write_text('x')
    assert engine.execute(command(kind='create', maxFiles=1))['errorCode'] == 'FILE_COUNT_QUOTA_EXCEEDED'


def test_expired_and_unknown_intent_do_not_mutate(tmp_path):
    engine = LocalProfiles(tmp_path, 'real')
    assert not engine.execute(command(kind='create', expiresAt='2000-01-01T00:00:00Z'))['success']


def test_hardlinks_rejected(tmp_path):
    engine = LocalProfiles(tmp_path, 'real')
    engine.execute(command())
    outside = tmp_path / 'outside.txt'
    outside.write_text('secret')
    desktop = tmp_path / 'real' / 'Devices' / DEVICE / 'Profiles' / USER / 'Desktop'
    (desktop / 'linked.txt').hardlink_to(outside)
    assert not engine.execute(command(kind='read', name='linked.txt'))['success']


def test_poll_retries_result_before_new_command_and_rechecks_authorization(tmp_path):
    from itam_agent import Agent, Config
    calls = []
    c = command(kind='create')
    fail = [True]
    def transport(path, body, credential):
        calls.append(path)
        if path.endswith('/poll'):
            return {'operation': c}
        if path.endswith('/authorize'):
            return {'allowed': True}
        if fail.pop() if fail else False:
            raise OSError('offline')
        return {'accepted': True}
    agent = Agent(Config('http://localhost:8080', 'real', tmp_path / 'state.json', True), transport)
    agent.state['credential'] = 'credential'
    engine = LocalProfiles(tmp_path / 'files', 'real')
    with pytest.raises(OSError):
        agent.file_step(engine)
    agent.file_step(engine)
    assert [p.rsplit('/', 1)[-1] for p in calls] == ['poll', 'authorize', 'result', 'result']
    assert len(engine.execute(command(kind='list'))['files']) == 1


def test_denied_authorization_does_not_touch_storage(tmp_path):
    from itam_agent import Agent, Config
    def transport(path, body, credential):
        return {'operation': command(kind='create')} if path.endswith('/poll') else {'allowed': False}
    agent = Agent(Config('http://localhost:8080', 'real', tmp_path / 'state.json', True), transport)
    agent.state['credential'] = 'credential'
    agent.file_step(LocalProfiles(tmp_path / 'files', 'real'))
    assert not (tmp_path / 'files').exists()


def test_quota_edit_move_copy_and_directory(tmp_path):
    engine = LocalProfiles(tmp_path, 'real')
    assert engine.execute(command(kind='create', maxFiles=1))['success']
    assert engine.execute(command(kind='mkdir', name='empty', maxFiles=1))['success']
    assert engine.execute(command(kind='write', maxFiles=1, contentBase64='aGk='))['success']
    assert engine.execute(command(kind='copy', newName='copy.txt', maxFiles=1))['errorCode'] == 'FILE_COUNT_QUOTA_EXCEEDED'
    moved = engine.execute(command(kind='move', targetFolder='Documents', maxFiles=1))
    assert moved['success'] and moved['files'][0]['folder'] == 'Documents'
    assert engine.execute(command(kind='copy', folder='Documents', newName='copy.txt', maxFiles=2))['success']


def test_file_id_operations_and_recycle_restore(tmp_path):
    engine = LocalProfiles(tmp_path, 'simulated')
    created = engine.execute(command(kind='create', contentBase64=base64.b64encode(b'first').decode()))
    file_id = created['files'][0]['fileId']

    read = engine.execute(command(kind='read', fileId=file_id, name=None, folder=None))
    assert base64.b64decode(read['contentBase64']) == b'first'
    assert engine.execute(command(kind='write', fileId=file_id, name=None, folder=None,
                                  contentBase64=base64.b64encode(b'updated').decode()))['success']
    assert engine.execute(command(kind='rename', fileId=file_id, name=None, folder=None,
                                  newName='renamed.txt'))['success']
    assert engine.execute(command(kind='move', fileId=file_id, name=None, folder=None,
                                  targetFolder='Documents'))['success']
    assert engine.execute(command(kind='copy', fileId=file_id, name=None, folder=None,
                                  newName='copy.txt', targetFolder='Desktop'))['success']
    assert engine.execute(command(kind='delete', fileId=file_id, name=None, folder=None))['success']

    restarted = LocalProfiles(tmp_path, 'simulated')
    trash = restarted.execute(command(kind='list', folder='RecycleBin'))
    assert len([item for item in trash['files'] if item['folder'] == 'RecycleBin']) == 1
    restored = restarted.execute(command(kind='restore', fileId=file_id, name=None, folder=None,
                                         targetFolder='Documents'))
    assert restored['success']
    assert any(item['fileId'] == file_id and item['folder'] == 'Documents' for item in restored['files'])
    assert not any(item['folder'] == 'RecycleBin' for item in restored['files'])


def test_recovery_only_and_interrupted_write_are_not_reexecuted(tmp_path, monkeypatch):
    engine = LocalProfiles(tmp_path, 'real')
    c = command(kind='create')
    assert engine.execute(dict(c, recoveryOnly=True))['errorCode'] == 'FILE_OPERATION_UNKNOWN'
    def fail(*args, **kwargs):
        raise OSError('disk failure')
    monkeypatch.setattr(engine, '_write', fail)
    assert engine.execute(c)['errorCode'] == 'FILE_OPERATION_UNKNOWN'
    restarted = LocalProfiles(tmp_path, 'real')
    assert restarted.execute(c)['errorCode'] == 'FILE_OPERATION_UNKNOWN'
    assert restarted.execute(command(kind='list'))['files'] == []


def test_profile_binding_and_binary_edit_rejected(tmp_path):
    engine = LocalProfiles(tmp_path, 'real')
    assert engine.execute(command(kind='create', name='report.pdf', contentBase64='JVBERg=='))['success']
    assert engine.execute(command(profileId=str(uuid.uuid4())))['errorCode'] == 'PROFILE_BINDING_MISMATCH'
    assert engine.execute(command(kind='write', name='report.pdf'))['errorCode'] == 'FILE_TYPE_NOT_EDITABLE'


def test_directory_symlink_rejected(tmp_path):
    engine = LocalProfiles(tmp_path, 'real')
    engine.execute(command())
    outside = tmp_path / 'outside'
    outside.mkdir()
    desktop = tmp_path / 'real' / 'Devices' / DEVICE / 'Profiles' / USER / 'Desktop'
    try:
        (desktop / 'escape').symlink_to(outside, target_is_directory=True)
    except OSError:
        pytest.skip('Windows symlink privilege unavailable')
    assert not engine.execute(command(kind='list'))['success']


def test_atomic_new_file_write_does_not_replace_existing_file(tmp_path):
    system = tmp_path / 'system'
    system.mkdir()
    destination = tmp_path / 'Desktop' / 'external.txt'
    destination.parent.mkdir()
    destination.write_bytes(b'keep')

    with pytest.raises(FileError, match='FILE_OPERATION_CONFLICT'):
        LocalProfiles._write(system, destination, b'overwrite')

    assert destination.read_bytes() == b'keep'
