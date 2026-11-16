"""Guards for the opt-in acceptance runner; these do not create or contact SQL databases."""
import ctypes
from ctypes import wintypes
import os
from pathlib import Path
import socket
import subprocess
import sys
import time

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from profile_e2e import check_ports, delete_own_database


@pytest.mark.parametrize('database', ['ITManagement', 'master', 'itam_profile_e2e_',
                                      'itam_profile_e2e_' + '0' * 32 + '; DROP DATABASE master'])
def test_cleanup_rejects_unowned_database_before_invoking_sql(monkeypatch, database):
    monkeypatch.setattr(subprocess, 'run', lambda *a, **kw: pytest.fail('SQL must not be invoked'))
    with pytest.raises(ValueError, match='not owned'):
        delete_own_database('unused', database)


def test_runner_refuses_application_and_busy_ports():
    for ports in ((5038, 5190), (5189, 5173), (5190, 5190)):
        with pytest.raises(ValueError):
            check_ports(*ports)
    with socket.socket() as listener:
        listener.bind(('127.0.0.1', 0))
        listener.listen()
        with pytest.raises(OSError):
            check_ports(listener.getsockname()[1])


@pytest.mark.skipif(os.name != 'nt', reason='Windows Job Object acceptance')
def test_windows_job_stops_descendant_when_runner_exits(tmp_path):
    source = '''
import os, pathlib, subprocess, sys, time
from profile_e2e import own_process_tree
own_process_tree()
root = pathlib.Path(sys.argv[1])
child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(60)'])
(root / 'pid').write_text(str(child.pid))
deadline = time.monotonic() + 15
while not (root / 'release').exists() and time.monotonic() < deadline: time.sleep(.05)
os._exit(0)
'''
    env = dict(os.environ, PYTHONPATH=str(Path(__file__).resolve().parents[1]))
    owner = subprocess.Popen([sys.executable, '-c', source, str(tmp_path)], env=env,
                             creationflags=subprocess.CREATE_NO_WINDOW)
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
    kernel.WaitForSingleObject.restype = wintypes.DWORD
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = None
    try:
        deadline = time.monotonic() + 10
        while not (tmp_path / 'pid').exists():
            assert owner.poll() is None and time.monotonic() < deadline
            time.sleep(.05)
        handle = kernel.OpenProcess(0x100000, False, int((tmp_path / 'pid').read_text()))
        assert handle, 'Child must exist before its owner exits'
        (tmp_path / 'release').write_text('release')
        assert owner.wait(timeout=10) == 0
        assert kernel.WaitForSingleObject(handle, 5000) == 0, 'Descendant survived the job owner'
    finally:
        if owner.poll() is None:
            owner.terminate()
            owner.wait(timeout=10)
        if handle:
            kernel.CloseHandle(handle)
