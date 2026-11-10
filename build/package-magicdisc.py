#!/usr/bin/env python3
"""Build a self-contained Mac app. Conversion engines are installed later by the user."""
from pathlib import Path
import os,plistlib,shutil,subprocess
import xml.etree.ElementTree as ET
root=Path(__file__).resolve().parent.parent
os.chdir(root)
dotnet=os.environ.get('MAGICDISC_DOTNET') or shutil.which('dotnet') or str(Path.home()/'.local/share/frameflow-dev/dotnet/dotnet')
publish=root/'build/magicdisc-publish'
subprocess.run([dotnet,'publish','src/Filee.App/Filee.App.csproj','-c','Release','-r','osx-arm64','--self-contained','true','-p:MagicDiscRelease=true','-o',str(publish),'-v:q'],check=True,env={**os.environ,'DOTNET_CLI_TELEMETRY_OPTOUT':'1'})
subprocess.run([dotnet,'run','--project','tools/MagicDisc.PackageCheck','-c','Release','--',str(publish/'MagicDisc.dll')],check=True,env={**os.environ,'DOTNET_CLI_TELEMETRY_OPTOUT':'1'})
bundle=Path(os.environ.get('MAGICDISC_BUNDLE_OUTPUT', str(root/'dist/MagicDisc.app'))).resolve()
if bundle.exists():shutil.rmtree(bundle)
mac=bundle/'Contents/MacOS';resources=bundle/'Contents/Resources'
shutil.copytree(publish,mac);resources.mkdir(parents=True)
# Never carry developer or system engine binaries into a distributable application.
assert not (mac/'engines').exists()
plist=plistlib.loads((root/'installer/macos/Info.plist').read_bytes())
for key in ['CFBundleName','CFBundleDisplayName','CFBundleExecutable']:plist[key]='MagicDisc'
plist['CFBundleIdentifier']='local.magicdisc.app'
plist['CFBundleShortVersionString']=plist['CFBundleVersion']=ET.parse(root/'Directory.Build.props').findtext('.//Version')
plist['CFBundleIconFile']='magicdisc.icns'
for key in ['NSAppleEventsUsageDescription','NSInputMonitoringUsageDescription']:plist[key]=plist[key].replace('Filee','MagicDisc')
for service in plist['NSServices']:
 service['NSMenuItem']['default']='Convert with MagicDisc';service['NSPortName']='MagicDisc'
(bundle/'Contents/Info.plist').write_bytes(plistlib.dumps(plist))
iconset=root/'build/.cache/magicdisc.iconset';iconset.mkdir(parents=True,exist_ok=True)
source=root/'src/Filee.App/Assets/Icons/filee.png'
for size in [16,32,128,256,512]:
 for scale in [1,2]:
  name=f'icon_{size}x{size}'+('@2x' if scale==2 else '')+'.png'
  subprocess.run(['sips','-z',str(size*scale),str(size*scale),str(source),'--out',str(iconset/name)],check=True,stdout=subprocess.DEVNULL)
subprocess.run(['iconutil','-c','icns',str(iconset),'-o',str(resources/'magicdisc.icns')],check=True)
for name in ['LICENSE','THIRD-PARTY-NOTICES.md','UPSTREAM.md']:shutil.copy2(root/name,resources/name)
subprocess.run(['codesign','--force','--deep','--sign','-','--entitlements',str(root/'installer/macos/entitlements.plist'),str(bundle)],check=True)
subprocess.run(['codesign','--verify','--deep','--strict',str(bundle)],check=True)
print(bundle)
