"""Isolated SQL/API/Edge acceptance for the server simulation. No Agent process is started."""
from pathlib import Path
import os
import secrets
import subprocess
import sys
import time
import uuid
import json

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'agent'))
from profile_e2e import (own_process_tree, current_localdb_pipe, check_ports, stop_process,
                         ready, request, delete_own_database, binary_fixtures, NO_WINDOW)

def main():
    own_process_tree()
    check_ports(5189, 5190)
    server = current_localdb_pipe()
    subprocess.run(['sqlcmd', '-S', server, '-E', '-b', '-l', '10', '-Q', 'SELECT 1'], check=True, capture_output=True, timeout=15)
    run = uuid.uuid4().hex
    database = 'itam_profile_e2e_' + run
    artifact = ROOT / 'artifacts/server-simulation-e2e' / run
    artifact.mkdir(parents=True)
    binary_fixtures(artifact)
    build = ROOT / 'artifacts/server-simulation/bin/ITManagement.API/debug'
    dll = build / 'ITManagement.API.dll'
    if not dll.is_file():
        raise RuntimeError('Build with dotnet build backend/ITManagement.slnx --artifacts-path artifacts/server-simulation first.')
    api_url, ui_url = 'http://127.0.0.1:5189', 'http://127.0.0.1:5190'
    password = secrets.token_urlsafe(24) + 'aA1!'
    logs, children = [], []
    summary = dict(passed=False, cleanup=False, checks=0)
    def start(command, env, name):
        log = (artifact / (name + '.log')).open('w', encoding='utf-8')
        logs.append(log)
        child = subprocess.Popen(command, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, creationflags=NO_WINDOW)
        children.append(child)
        return child
    try:
        env = os.environ.copy()
        env.update(ConnectionStrings__DefaultConnection=f'Server={server};Database={database};Integrated Security=True;Encrypt=False',
                   Database__InitializeOnStartup='true', SeedAdmin__Password=password,
                   Simulation__UseAgent='false', Simulation__StorageRoot=str(artifact / 'private-storage'),
                   ASPNETCORE_ENVIRONMENT='Development', ASPNETCORE_URLS=api_url,
                   Jwt__Secret=secrets.token_urlsafe(48), Jwt__Issuer='LiveTest', Jwt__Audience='LiveTest',
                   FileStorage__RootPath=str(artifact / 'legacy-storage'), Serilog__MinimumLevel__Default='Warning')
        command = ['dotnet', str(dll), '--contentRoot', str(ROOT / 'backend/src/ITManagement.API')]
        api = start(command, env, 'api')
        ready(api_url + '/swagger/index.html', api)
        admin = request(api_url, '/auth/login', dict(username='admin', password=password))['accessToken']
        user = request(api_url, '/users', dict(username='web_employee', password=password, email='web@test.local', fullName='Nhân viên kiểm thử', department='IT', role='Employee'), admin)
        assets = [request(api_url, '/assets', dict(assetCode=f'WEB-{i}', name=f'Máy mô phỏng {i}', serialNumber=f'SERIAL-{i}', model='Demo model'), admin) for i in (1, 2)]
        request(api_url, f"/assets/{assets[0]['id']}/specification", dict(operatingSystem='Windows 11 · mô phỏng', processor='CPU mô phỏng', memoryGb=16, diskTotalGb=512, diskFreeGb=256, diskEncryption=True,
            lastRestartedAt='2026-11-16T02:00:00Z', software=[dict(name='Ứng dụng mẫu', version='1.0', type='Mô phỏng', filePath='C:\\Program Files\\Demo\\app.exe')]), admin, 'PUT')
        assert request(api_url, '/agents', token=admin) == [], 'Acceptance must not register an Agent.'
        ui_env = os.environ.copy()
        ui_env.update(API_PROXY_TARGET=api_url, VITE_API_BASE_URL='/api')
        vite = start(['node', str(ROOT / 'frontend/admin/node_modules/vite/bin/vite.js'), str(ROOT / 'frontend/admin'), '--host', '127.0.0.1', '--port', '5190', '--strictPort'], ui_env, 'vite')
        ready(ui_url, vite)
        ui_env.update(ITAM_UI_URL=ui_url, ITAM_API_URL=api_url, ITAM_TEST_PASSWORD=password,
                      ITAM_DEVICE=assets[0]['id'], ITAM_OTHER_DEVICE=assets[1]['id'], ITAM_ARTIFACTS=str(artifact))
        browser = start(['node', str(ROOT / 'frontend/admin/tests/live/server-simulation.cjs')], ui_env, 'browser')
        deadline, restarted = time.monotonic() + 240, False
        while browser.poll() is None:
            if time.monotonic() > deadline: raise TimeoutError('Browser acceptance timeout')
            if (artifact / 'api-restart.request').exists() and not restarted:
                stop_process(api)
                env['Database__InitializeOnStartup'] = 'false'
                api = start(command, env, 'api-restarted')
                ready(api_url + '/swagger/index.html', api)
                (artifact / 'api-restart.done').write_text('ok')
                restarted = True
            if api.poll() is not None or vite.poll() is not None: raise RuntimeError('Test server stopped')
            time.sleep(.2)
        output = (artifact / 'browser.log').read_text(encoding='utf-8')
        print(output, end='')
        if browser.returncode: raise RuntimeError('Browser checks failed')
        assert restarted
        blobs = [p.read_bytes() for p in (artifact / 'private-storage').rglob('*') if p.is_file()]
        assert 'Nội dung lưu trên server 🌱'.encode() in blobs
        assert (artifact / 'sample.pdf').read_bytes() in blobs
        assert (artifact / 'sample.docx').read_bytes() in blobs
        summary.update(passed=True, checks=output.count('PASS ') + 1)
        print('PASS physical server blobs (text, PDF, DOCX)')
    finally:
        try:
            for child in reversed(children): stop_process(child)
            for log in logs: log.close()
            delete_own_database(server, database)
            summary['cleanup'] = True
        finally:
            (artifact / 'summary.json').write_text(json.dumps(summary, indent=2))
            print('Artifacts:', artifact)

if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    try: main()
    except Exception as error:
        print('FAIL:', type(error).__name__, file=sys.stderr)
        sys.exit(1)
