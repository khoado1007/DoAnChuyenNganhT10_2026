"""Explicit opt-in live smoke test against a disposable local test API.
Usage: python e2e_smoke.py --server http://127.0.0.1:5189 --consent-inventory
Requires ITAM_TEST_PASSWORD; never point at production.
"""
import argparse
import json
import os
from pathlib import Path
import tempfile
import urllib.request
import uuid
from itam_agent import Agent, Config, ProtocolError

parser = argparse.ArgumentParser()
parser.add_argument('--server', required=True)
parser.add_argument('--consent-inventory', action='store_true', required=True)
args = parser.parse_args()
Config(args.server, 'real', Path(tempfile.gettempdir()) / 'unused', args.consent_inventory)

def request(path, body=None, token=None):
    """Gửi yêu cầu JSON đến API kiểm thử và trả về phần dữ liệu phản hồi."""
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    req = urllib.request.Request(args.server + path, None if body is None else json.dumps(body).encode(), headers)
    with urllib.request.urlopen(req, timeout=15) as response:
        return json.load(response)['data']

jwt = request('/api/auth/login', dict(username='admin', password=os.environ['ITAM_TEST_PASSWORD']))['accessToken']
with tempfile.TemporaryDirectory(prefix='itam-agent-e2e-') as directory:
    identities = []
    clients = []
    for mode in ('real', 'simulated'):
        asset = request('/api/assets', dict(assetCode='E2E-' + mode + '-' + uuid.uuid4().hex[:8], name='E2E ' + mode), jwt)
        issued = request('/api/agents/enrollment-tokens', dict(assetId=asset['id'], mode=mode), jwt)
        agent = Agent(Config(args.server, mode, Path(directory) / (mode + '.json'), True))
        agent.enroll(issued['token'])
        agent.heartbeat()
        identities.append(agent.state['agentId'])
        clients.append(agent)
    rows = [row for row in request('/api/agents', token=jwt) if row['id'] in identities]
    assert len(rows) == 2
    assert {row['mode'] for row in rows} == {'real', 'simulated'}
    assert all(row['connectivity'] == 'online' for row in rows)
    assert len({row['instanceId'] for row in rows}) == 2
    assert clients[0].state['credential'] != clients[1].state['credential']
    request('/api/agents/' + identities[1] + '/disable', {}, jwt)
    try:
        clients[1].heartbeat()
        raise AssertionError('Disabled Agent heartbeat unexpectedly accepted')
    except ProtocolError as error:
        assert error.status == 403
    clients[0].heartbeat()
    print('PASS: real + simulated enroll/heartbeat, unique identities, server-side disable, other Agent unaffected')
