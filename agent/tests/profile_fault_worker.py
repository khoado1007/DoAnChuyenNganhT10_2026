"""Test-only worker: pause after the intent journal, before writing one fixture file.

Never imported by the production CLI. The E2E parent kills this process to simulate
an interrupted mutation; requests, credentials and actual payloads are not recorded.
"""
import os
from pathlib import Path
import re
import sys
import time

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from itam_agent import main
from local_profiles import LocalProfiles


if __name__ == '__main__':
    run_id = os.environ.get('ITAM_TEST_RUN', '')
    if not re.fullmatch('[0-9a-f]{32}', run_id):
        raise RuntimeError('This helper must be launched by the isolated E2E runner.')
    artifact = Path(os.environ['ITAM_ARTIFACTS']).resolve()
    expected = Path(__file__).resolve().parents[2] / 'artifacts/profile-e2e' / ('recovery-' + run_id)
    if artifact != expected.resolve():
        raise RuntimeError('Fault injection is restricted to its own test artifact root.')
    original_write = LocalProfiles._write

    def interrupted_write(system, destination, payload, overwrite=False):
        if destination.name == 'fault-before.txt':
            if not destination.resolve().is_relative_to(artifact / 'files'):
                raise RuntimeError('Refusing fault injection outside test storage.')
            (artifact / 'crash-ready').write_text('intent journal saved', encoding='utf-8')
            # The parent has a bounded timeout and terminates this child at this point.
            while True:
                time.sleep(.1)
        return original_write(system, destination, payload, overwrite)

    LocalProfiles._write = staticmethod(interrupted_write)
    raise SystemExit(main())
