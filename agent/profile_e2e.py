"""Windows acceptance tests using an isolated LocalDB, HTTP API, Edge and Python Agent.

Run from any directory: python agent/profile_e2e.py --suite all
Requires built frontend dependencies, Edge/Playwright, dotnet and sqlcmd.
Never accepts a business database name or application URL.
"""
from __future__ import annotations

import argparse
import ctypes
from ctypes import wintypes
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid
import zipfile

from itam_agent import Agent, Config

ROOT = Path(__file__).resolve().parents[1]
API_PROJECT = ROOT / 'backend/src/ITManagement.API'
API_BUILD = ROOT / 'artifacts/profile-e2e/api-build'
API_DLL = API_BUILD / 'ITManagement.API.dll'
VITE = ROOT / 'frontend/admin/node_modules/vite/bin/vite.js'
NO_WINDOW = getattr(subprocess, 'CREATE_NO_WINDOW', 0)
_JOB_HANDLE = None


def own_process_tree() -> None:
    """Keep runner and all descendants in a private kill-on-exit Windows job.

    The handle is deliberately held until process exit, not closed in finally:
    closing it early would kill the runner before its summary/DB cleanup completes.
    No existing parent, application or SQL process is assigned to this job.
    """
    global _JOB_HANDLE
    class Limits(ctypes.Structure):
        _fields_ = [('process_time', ctypes.c_int64), ('job_time', ctypes.c_int64),
                    ('flags', wintypes.DWORD), ('min_working_set', ctypes.c_size_t),
                    ('max_working_set', ctypes.c_size_t), ('active_limit', wintypes.DWORD),
                    ('affinity', ctypes.c_size_t), ('priority', wintypes.DWORD), ('scheduling', wintypes.DWORD)]
    class IO(ctypes.Structure):
        _fields_ = [(name, ctypes.c_uint64) for name in
                    ('read_ops', 'write_ops', 'other_ops', 'read_bytes', 'write_bytes', 'other_bytes')]
    class ExtendedLimits(ctypes.Structure):
        _fields_ = [('basic', Limits), ('io', IO), ('process_memory', ctypes.c_size_t),
                    ('job_memory', ctypes.c_size_t), ('peak_process', ctypes.c_size_t),
                    ('peak_job', ctypes.c_size_t)]
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.CreateJobObjectW.argtypes = [ctypes.c_void_p, wintypes.LPCWSTR]
    kernel.CreateJobObjectW.restype = wintypes.HANDLE
    kernel.SetInformationJobObject.argtypes = [wintypes.HANDLE, ctypes.c_int, ctypes.c_void_p, wintypes.DWORD]
    kernel.SetInformationJobObject.restype = wintypes.BOOL
    kernel.AssignProcessToJobObject.argtypes = [wintypes.HANDLE, wintypes.HANDLE]
    kernel.AssignProcessToJobObject.restype = wintypes.BOOL
    kernel.GetCurrentProcess.restype = wintypes.HANDLE
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    job = kernel.CreateJobObjectW(None, None)
    if not job:
        raise ctypes.WinError(ctypes.get_last_error())
    info = ExtendedLimits()
    info.basic.flags = 0x2000  # JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
    if not kernel.SetInformationJobObject(job, 9, ctypes.byref(info), ctypes.sizeof(info)):
        error = ctypes.WinError(ctypes.get_last_error())
        kernel.CloseHandle(job)
        raise error
    if not kernel.AssignProcessToJobObject(job, kernel.GetCurrentProcess()):
        error = ctypes.WinError(ctypes.get_last_error())
        kernel.CloseHandle(job)
        raise error
    _JOB_HANDLE = job


def current_localdb_pipe() -> str:
    """Use the currently running instance; never start/reset SQL Server ourselves."""
    log = (Path(os.environ['LOCALAPPDATA']) / 'Microsoft/Microsoft SQL Server Local DB'
           / 'Instances/MSSQLLocalDB/error.log')
    matches = re.findall(r'(\\\\\.\\pipe\\LOCALDB#[^\s\]]+)', log.read_text(encoding='utf-16'))
    if not matches:
        raise RuntimeError('Open SSMS and connect to (localdb)\\MSSQLLocalDB first.')
    return 'np:' + matches[-1]


def check_ports(*ports: int) -> None:
    if len(set(ports)) != len(ports):
        raise ValueError('API and UI ports must differ.')
    for port in ports:
        if not 1024 <= port <= 65535 or port in (5038, 5173):
            raise ValueError('Choose test ports 1024..65535, excluding application ports 5038/5173.')
        with socket.socket() as sock:
            sock.bind(('127.0.0.1', port))


def stop_process(process: subprocess.Popen) -> None:
    if process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=15)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=10)


def ready(url: str, process: subprocess.Popen, timeout: int = 90) -> None:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError('Child process exited before startup; inspect its log.')
        try:
            with urllib.request.urlopen(url, timeout=2) as response:
                if response.status == 200:
                    return
        except (OSError, urllib.error.URLError):
            time.sleep(.3)
    raise TimeoutError(f'Application did not become ready: {url}')


def request(server: str, route: str, body=None, token=None, method=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    req = urllib.request.Request(server + '/api' + route,
                                 None if body is None else json.dumps(body).encode(),
                                 headers, method=method)
    with urllib.request.urlopen(req, timeout=25) as response:
        return json.load(response)['data']


def delete_own_database(sql_server: str, database: str) -> None:
    if not re.fullmatch(r'itam_profile_e2e_[0-9a-f]{32}', database):
        raise ValueError('Refusing cleanup of a database not owned by this test run.')
    # Only the generated identifier is interpolated; never accept a database from CLI/env.
    query = (f"IF DB_ID(N'{database}') IS NOT NULL BEGIN "
             f'ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; '
             f'DROP DATABASE [{database}]; END')
    subprocess.run(['sqlcmd', '-S', sql_server, '-E', '-b', '-l', '10', '-Q', query],
                   check=True, timeout=30, capture_output=True, creationflags=NO_WINDOW)


def binary_fixtures(directory: Path) -> None:
    """Small valid containers for byte-for-byte upload/download checks, not personal files."""
    text = b'BT /F1 12 Tf 40 80 Td (IT Desk binary test) Tj ET'
    objects = [b'<< /Type /Catalog /Pages 2 0 R >>',
               b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
               b'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 150] '
               b'/Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>',
               b'<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
               b'<< /Length ' + str(len(text)).encode() + b' >>\nstream\n' + text + b'\nendstream']
    pdf, offsets = bytearray(b'%PDF-1.4\n'), [0]
    for index, body in enumerate(objects, 1):
        offsets.append(len(pdf))
        pdf.extend(str(index).encode() + b' 0 obj\n' + body + b'\nendobj\n')
    xref = len(pdf)
    pdf.extend(b'xref\n0 6\n0000000000 65535 f \n')
    for offset in offsets[1:]:
        pdf.extend(f'{offset:010d} 00000 n \n'.encode())
    pdf.extend(f'trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n'.encode())
    (directory / 'sample.pdf').write_bytes(pdf)
    with zipfile.ZipFile(directory / 'sample.docx', 'w', zipfile.ZIP_DEFLATED) as archive:
        archive.writestr('[Content_Types].xml',
            '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
            '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
            '<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>')
        archive.writestr('_rels/.rels',
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>')
        archive.writestr('word/document.xml',
            '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">'
            '<w:body><w:p><w:r><w:t>IT Desk binary test</w:t></w:r></w:p></w:body></w:document>')


def run_suite(args, suite: str, sql_server: str) -> dict:
    check_ports(args.api_port, args.ui_port)
    run_id = uuid.uuid4().hex
    database = 'itam_profile_e2e_' + run_id
    artifact = ROOT / 'artifacts/profile-e2e' / (suite + '-' + run_id)
    artifact.mkdir(parents=True)
    binary_fixtures(artifact)
    password = secrets.token_urlsafe(24) + 'aA1!'
    server = f'http://127.0.0.1:{args.api_port}'
    ui = f'http://127.0.0.1:{args.ui_port}'
    processes, logs = [], []
    result = dict(suite=suite, database=database, passed=False, cleanup=False, checks=0)
    print(f'{suite}: {artifact}', flush=True)

    def start(command, env, name):
        log = (artifact / (name + '.log')).open('w', encoding='utf-8')
        logs.append(log)
        process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=log,
                                   stderr=subprocess.STDOUT, creationflags=NO_WINDOW)
        processes.append(process)
        return process

    try:
        env = os.environ.copy()
        env.update(ConnectionStrings__DefaultConnection=(
            f'Server={sql_server};Initial Catalog={database};Integrated Security=True;'
            'Encrypt=False;Pooling=True;Connect Timeout=10'),
            Database__InitializeOnStartup='true', SeedAdmin__Password=password,
            ASPNETCORE_ENVIRONMENT='Development', ASPNETCORE_URLS=server,
            Jwt__Secret=secrets.token_urlsafe(48), Jwt__Issuer='LiveTest', Jwt__Audience='LiveTest',
            FileStorage__RootPath=str(artifact / 'api-storage'),
            Serilog__MinimumLevel__Default='Warning')
        api = start(['dotnet', str(API_DLL), '--contentRoot', str(API_PROJECT)], env, 'api')
        ready(server + '/swagger/index.html', api)
        admin = request(server, '/auth/login', dict(username='admin', password=password))['accessToken']
        users = []
        for name in ('live_employee', 'live_other'):
            users.append(request(server, '/users', dict(username=name, password=password,
                email=name + '@test.local', fullName=name, department='Test', role='Employee'), admin))
        asset = request(server, '/assets', dict(assetCode='LIVE-TEST', name='LIVE TEST PC'), admin)
        issued = request(server, '/agents/enrollment-tokens', dict(assetId=asset['id'], mode='simulated'), admin)
        state = artifact / 'state.json'
        Agent(Config(server, 'simulated', state, False)).enroll(issued['token'])
        request(server, f"/assets/{asset['id']}/access/{users[0]['id']}", {}, admin, 'PUT')
        agent_args = [sys.executable, str(ROOT / 'agent/itam_agent.py'), 'serve-files',
                      '--server', server, '--mode', 'simulated', '--state', str(state),
                      '--root', str(artifact / 'files')]
        worker_env = dict(os.environ, ITAM_TEST_RUN=run_id, ITAM_ARTIFACTS=str(artifact))
        first_worker_args = list(agent_args)
        if suite == 'recovery':
            first_worker_args[1] = str(ROOT / 'agent/tests/profile_fault_worker.py')
        worker = start(first_worker_args, worker_env, 'agent-before')
        ui_env = os.environ.copy()
        ui_env['API_PROXY_TARGET'] = server
        # Keep this test process independent of any developer's VITE_API_BASE_URL.
        ui_env['VITE_API_BASE_URL'] = '/api'
        vite = start(['node', str(VITE), str(ROOT / 'frontend/admin'), '--host', '127.0.0.1',
                      '--port', str(args.ui_port), '--strictPort'], ui_env, 'vite')
        ready(ui, vite)
        ui_env.update(ITAM_TEST_RUN=run_id, ITAM_API_URL=server, ITAM_UI_URL=ui,
                      ITAM_TEST_PASSWORD=password, ITAM_ADMIN_TOKEN=admin,
                      ITAM_DEVICE=asset['id'], ITAM_ARTIFACTS=str(artifact))
        script = ROOT / f'frontend/admin/tests/live/profile-{suite}.cjs'
        browser = start(['node', str(script)], ui_env, 'browser')
        deadline, restarted, api_restarted = time.monotonic() + 360, False, False
        crashed = False
        while browser.poll() is None:
            if time.monotonic() > deadline:
                raise TimeoutError('Browser suite exceeded 360 seconds.')
            for child in (api, vite, worker):
                if child.poll() is not None:
                    raise RuntimeError('API/Vite/Agent stopped during the test; inspect logs.')
            if (artifact / 'crash-ready').exists() and not crashed:
                stop_process(worker)
                worker = start(agent_args, worker_env, 'agent-after-crash')
                crashed = True
                (artifact / 'crash-restarted').write_text('restarted', encoding='utf-8')
            if (artifact / 'restart.request').exists() and not restarted:
                stop_process(worker)
                worker = start(agent_args, os.environ.copy(), 'agent-after')
                restarted = True
                (artifact / 'restart.done').write_text('restarted', encoding='utf-8')
            if (artifact / 'api-restart.request').exists() and not api_restarted:
                stop_process(api)
                restart_env = dict(env, Database__InitializeOnStartup='false')
                api = start(['dotnet', str(API_DLL), '--contentRoot', str(API_PROJECT)], restart_env, 'api-after')
                ready(server + '/swagger/index.html', api)
                api_restarted = True
                (artifact / 'api-restart.done').write_text('restarted', encoding='utf-8')
            time.sleep(.2)
        output = (artifact / 'browser.log').read_text(encoding='utf-8')
        print(output, end='', flush=True)
        if browser.returncode:
            raise RuntimeError(f'{suite} browser checks failed; see {artifact / "browser.log"}')
        profile = artifact / 'files/simulated/Devices' / asset['id'] / 'Profiles' / users[0]['id']
        if suite == 'core':
            assert (profile / 'Desktop/live-note.txt').read_text(encoding='utf-8') == (
                'Saved through real Python Agent — kiểm thử')
        elif suite == 'files':
            assert restarted, 'Agent restart was not exercised.'
            assert api_restarted, 'API restart was not exercised.'
            for name in ('renamed.txt', 'copy.txt'):
                assert (profile / 'Documents' / name).read_text(encoding='utf-8') == 'Extended UTF-8 — nội dung'
            assert (profile / 'Desktop/import.csv').read_bytes() == b'name,value\nalpha,42\n'
            assert len(list(profile.rglob('idempotent.txt'))) == 1
            for name in ('sample.pdf', 'sample.docx'):
                assert (profile / 'Desktop' / name).read_bytes() == (artifact / name).read_bytes()
        else:
            assert crashed, 'Interrupted mutation was not exercised.'
            assert not list(profile.rglob('fault-before.txt')), 'Interrupted intent unexpectedly wrote a file.'
            system = artifact / 'files/simulated/Devices' / asset['id'] / 'System' / users[0]['id']
            assert any(json.loads(p.read_text(encoding='utf-8')).get('result', 'missing') is None
                       for p in system.glob('*.json')), 'Interrupted intent journal missing.'
            assert (profile / 'Desktop/kept.txt').read_bytes() == b'keep this document'
            assert (profile / 'Desktop/after-reconcile.txt').is_file()
        result['checks'] = len(re.findall(r'^PASS ', output, re.MULTILINE)) + 1
        result['passed'] = True
        print('PASS: physical disk content verified', flush=True)
    finally:
        # Stop only children started by this run, never existing application/SQL processes.
        try:
            cleanup_errors = []
            for process in reversed(processes):
                try:
                    stop_process(process)
                except (OSError, subprocess.TimeoutExpired) as error:
                    cleanup_errors.append(type(error).__name__)
            for log in logs:
                log.close()
            delete_own_database(sql_server, database)
            result['cleanup'] = not cleanup_errors
            if cleanup_errors:
                raise RuntimeError('Some children did not stop normally; Windows job will terminate them on exit.')
        finally:
            (artifact / 'summary.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
        print('Test processes stopped; own GUID database deleted.', flush=True)
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--suite', choices=('core', 'files', 'recovery', 'all'), default='all')
    parser.add_argument('--api-port', type=int, default=5189)
    parser.add_argument('--ui-port', type=int, default=5190)
    parser.add_argument('--no-build', action='store_true', help='Reuse the API previously built by this runner.')
    args = parser.parse_args()
    if os.name != 'nt':
        raise RuntimeError('This runner currently supports Windows + LocalDB only.')
    own_process_tree()
    for command in ('dotnet', 'node', 'sqlcmd'):
        if not shutil.which(command):
            raise RuntimeError(f'Required tool missing from PATH: {command}')
    check_ports(args.api_port, args.ui_port)
    if not VITE.is_file():
        raise RuntimeError('Run npm --prefix frontend/admin ci first.')
    subprocess.run(['node', '-e', "require(process.env.PLAYWRIGHT_MODULE || 'playwright')"],
                   cwd=ROOT / 'frontend/admin', check=True, timeout=15, creationflags=NO_WINDOW)
    sql_server = current_localdb_pipe()
    subprocess.run(['sqlcmd', '-S', sql_server, '-E', '-b', '-l', '10', '-Q', 'SELECT 1'],
                   check=True, timeout=15, capture_output=True, creationflags=NO_WINDOW)
    if not args.no_build:
        subprocess.run(['dotnet', 'build', str(API_PROJECT), '--output', str(API_BUILD),
                        '--nologo', '--verbosity', 'quiet'],
                       cwd=ROOT, check=True, timeout=180, creationflags=NO_WINDOW)
    if not API_DLL.is_file():
        raise RuntimeError('API DLL missing; run without --no-build.')
    suites = ('core', 'files', 'recovery') if args.suite == 'all' else (args.suite,)
    results = [run_suite(args, suite, sql_server) for suite in suites]
    print(f"PASS: {sum(r['checks'] for r in results)} checks across {len(results)} suites.")


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')
    try:
        main()
    except (Exception, KeyboardInterrupt) as error:
        print(f'FAIL: {error}', file=sys.stderr)
        sys.exit(1)
