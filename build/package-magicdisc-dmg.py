#!/usr/bin/env python3
"""Package the current app with macOS signing metadata preserved."""
from pathlib import Path
import plistlib
import subprocess
import tempfile

root = Path(__file__).resolve().parent.parent
bundle = root / 'dist/MagicDisc.app'
version = plistlib.loads((bundle / 'Contents/Info.plist').read_bytes())['CFBundleShortVersionString']
subprocess.run(['codesign', '--verify', '--deep', '--strict', str(bundle)], check=True)
with tempfile.TemporaryDirectory(prefix='magicdisc-dmg-') as temporary:
    stage = Path(temporary)
    # ditto preserves resource forks / extended attributes used to sign managed DLLs on macOS.
    subprocess.run(['ditto', str(bundle), str(stage / 'MagicDisc.app')], check=True)
    subprocess.run(['codesign', '--verify', '--deep', '--strict', str(stage / 'MagicDisc.app')], check=True)
    (stage / 'Applications').symlink_to('/Applications')
    subprocess.run(['hdiutil', 'create', '-volname', 'MagicDisc', '-srcfolder', str(stage),
                    '-ov', '-format', 'UDZO', str(root / f'dist/MagicDisc-{version}-macOS-arm64.dmg')], check=True)
