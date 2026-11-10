import json
from pathlib import Path
import sys
import pytest
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from itam_agent import Config, Agent, ProtocolError, load_inventory, retry_delay


def config(tmp_path, **kwargs):
    """Tạo cấu hình kiểm thử mặc định, cho phép ghi đè các trường chỉ định."""
    values = dict(server='http://127.0.0.1:8080', mode='simulated', state=tmp_path / 'state.json', consent=True, interval=30)
    values.update(kwargs)
    return Config(**values)


@pytest.mark.parametrize('kwargs', [dict(server='http://example.com'), dict(mode='other'), dict(consent=False), dict(interval=0), dict(server='https://user:password@example.com'), dict(server='https://example.com/path')])
def test_rejects_unsafe_config(tmp_path, kwargs):
    """Xác nhận cấu hình không an toàn bị từ chối bằng ValueError."""
    with pytest.raises(ValueError):
        config(tmp_path, **kwargs)


def test_fixture_is_explicitly_synthetic(tmp_path):
    """Kiểm tra dữ liệu giả lập có tiền tố nhận diện và trường kiểm kê hợp lệ."""
    inventory = load_inventory(config(tmp_path))
    assert inventory['hostname'].startswith('SIM-')
    assert inventory['ramBytes'] > 0
    assert inventory['protocolVersion'] == 1


def test_enrollment_is_not_retried_and_credentials_are_scoped(tmp_path):
    """Kiểm tra đăng ký chỉ gọi một lần và thông tin xác thực gắn với cấu hình."""
    calls = []
    def transport(path, body, credential):
        """Ghi nhận yêu cầu và trả kết quả đăng ký giả lập."""
        calls.append(path)
        return dict(agentId='agent-a', credential='secret-a', heartbeatIntervalSeconds=30)
    agent = Agent(config(tmp_path), transport=transport)
    agent.enroll('one-use')
    assert calls == ['/api/agents/enroll']
    with pytest.raises(ValueError):
        Agent(config(tmp_path, mode='real'), transport=transport)
    with pytest.raises(ValueError):
        agent.enroll('another')
    assert 'one-use' not in (tmp_path / 'state.json').read_text()


def test_recovery_re_enroll_rotates_instance_id_and_updates_credentials(tmp_path):
    """Kiểm tra đăng ký lại đổi danh tính, token và thông tin xác thực."""
    enrollments = [
        dict(agentId='agent-1', credential='secret-1', heartbeatIntervalSeconds=30),
        dict(agentId='agent-2', credential='secret-2', heartbeatIntervalSeconds=30),
    ]
    instances_seen = []
    tokens_seen = []

    def transport(path, body, credential):
        """Giả lập phản hồi đăng ký và nhịp tim theo thứ tự."""
        if path == '/api/agents/enroll':
            instances_seen.append(body['instanceId'])
            tokens_seen.append(body['token'])
            return enrollments.pop(0)
        return dict(lastSeenAt=None, lastSequence=body['sequence'])

    agent = Agent(config(tmp_path), transport=transport)
    agent.enroll('token-initial')
    first_instance = agent.state['instanceId']
    assert agent.state['credential'] == 'secret-1'

    # Cannot overwrite with standard enroll
    with pytest.raises(ValueError):
        agent.enroll('token-recovery')

    # Re-enroll resets instance ID and updates credential
    agent.re_enroll('token-recovery')
    second_instance = agent.state['instanceId']
    assert first_instance != second_instance
    assert tokens_seen == ['token-initial', 'token-recovery']
    assert instances_seen == [first_instance, second_instance]
    assert agent.state['credential'] == 'secret-2'
    assert agent.state['agentId'] == 'agent-2'
    assert agent.state['revoked'] is False

    # New credential sends heartbeats successfully
    heartbeat_credentials = []
    def heartbeat_transport(path, body, credential):
        """Ghi nhận thông tin xác thực dùng trong nhịp tim và trả phản hồi."""
        heartbeat_credentials.append(credential)
        return dict(lastSeenAt=None, lastSequence=body['sequence'])

    agent.transport = heartbeat_transport
    agent.heartbeat()
    assert heartbeat_credentials == ['secret-2']


def test_retry_reuses_sequence_and_disable_stops(tmp_path):
    """Kiểm tra thử lại dùng cùng số thứ tự và Agent dừng sau khi bị vô hiệu."""
    outcomes = [TimeoutError(), {}, ProtocolError(403)]
    seen = []
    def transport(path, body, credential):
        """Phát lần lượt lỗi hoặc phản hồi giả lập và ghi lại nhịp tim."""
        if path.endswith('enroll'):
            return dict(agentId='id', credential='secret')
        seen.append(body.copy())
        result = outcomes.pop(0)
        if isinstance(result, Exception):
            raise result
        return result
    agent = Agent(config(tmp_path), transport=transport)
    agent.enroll('token')
    with pytest.raises(TimeoutError):
        agent.heartbeat()
    agent.heartbeat()
    assert seen[0] == seen[1]
    assert seen[0]['sequence'] == 1
    with pytest.raises(ProtocolError):
        agent.heartbeat()
    with pytest.raises(ValueError):
        agent.heartbeat()
    assert len(seen) == 3


def test_backoff_is_bounded():
    """Xác nhận thời gian chờ thử lại nằm trong giới hạn dự kiến."""
    assert 30 <= retry_delay(0, 30) <= 37.5
    assert 30 <= retry_delay(10000, 30) <= 300


def test_instance_lock_releases_on_process_death_and_excludes_second_owner(tmp_path):
    """Kiểm tra khóa loại trừ tiến trình thứ hai và được giải phóng khi tiến trình chết."""
    import subprocess
    import itam_agent
    lock_path = tmp_path / 'instance.lock'
    child = subprocess.Popen([sys._base_executable, '-c',
        "import sys,time; sys.path.insert(0, sys.argv[1]); from itam_agent import acquire_instance_lock; lock=acquire_instance_lock(Path(sys.argv[2]))".replace('lock=acquire', 'from pathlib import Path; lock=acquire') + "; print('ready', flush=True); time.sleep(60)",
        str(Path(__file__).resolve().parents[1]), str(lock_path)], stdout=subprocess.PIPE, text=True)
    try:
        assert child.stdout.readline().strip() == 'ready'
        with pytest.raises(OSError):
            itam_agent.acquire_instance_lock(lock_path)
    finally:
        child.kill()
        child.wait()
    with itam_agent.acquire_instance_lock(lock_path):
        pass


def test_pending_heartbeat_survives_restart(tmp_path):
    """Kiểm tra nhịp tim đang chờ được gửi lại sau khi khởi động lại Agent."""
    def unavailable(path, body, credential):
        """Giả lập lỗi mạng cho nhịp tim nhưng cho phép đăng ký."""
        if path.endswith('enroll'):
            return dict(agentId='id', credential='secret')
        raise TimeoutError()
    first = Agent(config(tmp_path), transport=unavailable)
    first.enroll('token')
    with pytest.raises(TimeoutError):
        first.heartbeat()
    pending = first.state['pending'].copy()
    sent = []
    second = Agent(config(tmp_path), transport=lambda path, body, credential: sent.append(body.copy()) or {})
    second.heartbeat()
    assert sent == [pending]
    second.heartbeat()
    assert sent[1]['sequence'] == pending['sequence'] + 1


def test_runtime_reconnect_backoff_and_terminal_revocation(tmp_path):
    """Kiểm tra lùi thời gian thử lại và thu hồi khi nhận lỗi xác thực cuối."""
    outcomes = [TimeoutError(), ProtocolError(503), {}, ProtocolError(401)]
    def transport(path, body, credential):
        """Phát lần lượt lỗi hoặc phản hồi để mô phỏng các lần kết nối lại."""
        if path.endswith('enroll'):
            return dict(agentId='id', credential='secret')
        result = outcomes.pop(0)
        if isinstance(result, Exception):
            raise result
        return result
    class Stop:
        """Thay thế sự kiện dừng để lưu các khoảng chờ của vòng lặp Agent."""

        delays = []

        def is_set(self):
            """Báo vòng lặp chưa được yêu cầu dừng."""
            return False

        def wait(self, seconds):
            """Ghi lại thời gian chờ do vòng lặp Agent yêu cầu."""
            self.delays.append(seconds)
    agent = Agent(config(tmp_path), transport=transport)
    agent.enroll('token')
    stop = Stop()
    assert agent.run(stop) == 1
    assert 30 <= stop.delays[0] <= 37.5
    assert 60 <= stop.delays[1] <= 75
    assert stop.delays[2] == 30
    assert agent.state['revoked'] is True
