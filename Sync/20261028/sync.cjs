const fs = require('fs'), path = require('path'), crypto = require('crypto');
const src = path.resolve(__dirname,'../..'), dst = path.resolve(src,'..');
if (dst.toLowerCase() !== 'c:\\svn\\game') throw Error('Unexpected target');
const walk = d => fs.existsSync(d) ? fs.readdirSync(d,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(d,e.name)):[path.join(d,e.name)]) : [];
const read = f => fs.readFileSync(f,'utf8');
const writes = new Map(), deletes = new Set();
function put(rel,data) {
  const file = path.resolve(dst,rel); if (!file.startsWith(dst+path.sep) || file.startsWith(src+path.sep)) throw Error('Unsafe write');
  data = Buffer.isBuffer(data)?data:Buffer.from(data);
  if (!fs.existsSync(file) || !fs.readFileSync(file).equals(data)) writes.set(file,data);
}
function remove(rel) { const file=path.resolve(dst,rel); if(!file.startsWith(dst+path.sep)||file.startsWith(src+path.sep))throw Error('Unsafe removal'); if(fs.existsSync(file))deletes.add(file); }
const guids = new Map();
for (const f of walk(path.join(dst,'Assets')).filter(f=>f.endsWith('.meta'))) { const guid=read(f).match(/^guid: (\w+)/m)?.[1];if(guid)guids.set(guid,f.slice(0,-5)); }
const locations = {AstarPathfindingProject:'Assets/AstarPathfindingProject',Spine:'Assets/Spine',UniTask:'Assets/Plugins/UniTask',FancyScrollView:'Assets/Plugins/FancyScrollView',Demigiant:'Assets/Plugins/Demigiant',Concentus:'Assets/Plugins/Concentus','Google.Protobuf.3.35.1':'Assets/Plugins/Google.Protobuf.3.35.1','System.Buffers.4.4.0':'Assets/Plugins/System.Buffers.4.4.0','System.Memory.4.5.3':'Assets/Plugins/System.Memory.4.5.3','System.Numerics.Vectors.4.4.0':'Assets/Plugins/System.Numerics.Vectors.4.4.0','System.Runtime.CompilerServices.Unsafe.6.1.2':'Assets/Plugins/System.Runtime.CompilerServices.Unsafe.6.1.2'};
const moved=[];
for(const f of walk(path.join(src,'Assets/FrameWork'))) {
  const rel=path.relative(src,f).replaceAll('\\','/');
  if(f.endsWith('.meta') || /Assets\/FrameWork\/(Asset|Resources|Scenes|Scripts|Generated|YooAssetSettings)\//.test(rel))continue;
  if(!/\.(cs|asmdef|asmref|dll|json|shader|tpl|h|cpp|inc)$/.test(rel))continue;
  const isPlugin=rel.startsWith('Assets/FrameWork/Plugins/');
  const meta=f+'.meta',guid=fs.existsSync(meta)&&read(meta).match(/^guid: (\w+)/m)?.[1];
  const old=guid&&guids.get(guid);
  let dest=rel;
  if(isPlugin) {
    const parts=rel.split('/'),name=parts[3];
    if(locations[name])dest=locations[name]+'/'+parts.slice(4).join('/');
    if(old)dest=path.relative(dst,old);
  } else if(old&&path.resolve(old)!==path.resolve(dst,dest)) {
    moved.push({from:path.relative(dst,old),to:dest});
    remove(path.relative(dst,old));remove(path.relative(dst,old)+'.meta');
  }
  put(dest,fs.readFileSync(f));
  if(!fs.existsSync(path.join(dst,dest+'.meta')) && fs.existsSync(meta))
    put(dest+'.meta',old&&fs.existsSync(old+'.meta')?fs.readFileSync(old+'.meta'):fs.readFileSync(meta));
}
// Replace the old framework/runtime split with the current bootstrap/hot-update split.
for(const rel of ['Assets/FrameWork/FrameWork.Runtime.asmdef','Assets/FrameWork/Scripts/HotUpdate.asmdef']) { remove(rel);remove(rel+'.meta'); }
const oldHotMeta=path.join(dst,'Assets/FrameWork/Scripts/HotUpdate.asmdef.meta');
if(fs.existsSync(oldHotMeta))put('Assets/FrameWork/HotUpdate.asmdef.meta',fs.readFileSync(oldHotMeta));
const hot=JSON.parse(read(path.join(src,'Assets/FrameWork/HotUpdate.asmdef')));
hot.references.push('Obfuz.Runtime','Coffee.UnmaskForUGUI','GUID:5efd170ecd8084500bed5692932fe14e');
hot.references=[...new Set(hot.references)];put('Assets/FrameWork/HotUpdate.asmdef',JSON.stringify(hot,null,2)+'\n');
const editor=JSON.parse(read(path.join(src,'Assets/FrameWork/Editor/FrameWork.Editor.asmdef')));
editor.references.push('GUID:eb6ae48726f0b434e9d6ea7d36a67545','GUID:5efd170ecd8084500bed5692932fe14e');
editor.references=[...new Set(editor.references)];put('Assets/FrameWork/Editor/FrameWork.Editor.asmdef',JSON.stringify(editor,null,2)+'\n');
for(const rel of ['Assets/Obfuz/HotUpdate.asmref','Assets/Plugins/Demigiant/DOTween/Modules/FrameWork.Runtime.asmref'])
  put(rel,JSON.stringify({reference:'HotUpdate'},null,2)+'\n');
// The alias resolver is required by the AOT asset loader; retain the game's existing aliases.
const aliases='Assets/FrameWork/Tool/Resources/GameResourceAliases.cs';
if(fs.existsSync(path.join(dst,aliases))) {
  for(const suffix of ['','.meta']) {if(fs.existsSync(path.join(dst,aliases+suffix)))put('Assets/FrameWork/Tool/Bootstrap/GameResourceAliases.cs'+suffix,fs.readFileSync(path.join(dst,aliases+suffix)));remove(aliases+suffix);}
}
const loader='Assets/FrameWork/Tool/Bootstrap/AbMrg.cs';
let content=read(path.join(src,loader)).replace('Package.LoadAssetAsync(name, typeof(T))','Package.LoadAssetAsync(GameResourceAliases.Resolve(name, typeof(T)), typeof(T))').replace('Package.LoadAssetSync(name, typeof(T))','Package.LoadAssetSync(GameResourceAliases.Resolve(name, typeof(T)), typeof(T))').replace('Package.LoadAssetAsync<GameObject>(name)','Package.LoadAssetAsync<GameObject>(GameResourceAliases.Resolve(name, typeof(GameObject)))');
put(loader,content);
// Relocate actual game configuration without copying the framework's sample values.
for(const suffix of ['','.meta']) {const old='Assets/FrameWork/Resources/ConfigData.asset'+suffix;if(fs.existsSync(path.join(dst,old))){put('Assets/FrameWork/Asset/Base/ConfigData.asset'+suffix,fs.readFileSync(path.join(dst,old)));remove(old);}}
const entry='Assets/FrameWork/Scripts/GameEntry.cs';
content=read(path.join(dst,entry));
if(!content.includes('await Config.InitializeAsync();'))content=content.replace('public static async UniTask Start()\n        {','public static async UniTask Start()\n        {\n            await Config.InitializeAsync();').replace('public static async UniTask Start()\r\n        {','public static async UniTask Start()\r\n        {\r\n            await Config.InitializeAsync();');
content=content.replace('UiManager.Init(new UiRoot());','await UiManager.InitAsync(new UiRoot());');put(entry,content);
// Preserve the integrated project's layout checks and unique-resource addressing.
const setup='Assets/FrameWork/Editor/FrameworkSetup.cs';content=read(path.join(src,setup));
content=content.replace(/            foreach \(string directory in Directory.GetDirectories\("Assets"\)\)[\s\S]*?throw new InvalidOperationException\("发现 FrameWork 外的资产目录：" \+ directory\);/,'            foreach (string required in new[] { "Assets/FrameWork/Tool/Bootstrap/FrameWork.Bootstrap.asmdef", "Assets/FrameWork/HotUpdate.asmdef", FrameworkIntegrationEditor.BootScenePath })\n                if (!File.Exists(required)) throw new InvalidOperationException("框架文件缺失：" + required);');put(setup,content);
const collector='Assets/FrameWork/Editor/YooResourceEditor.cs';content=read(path.join(src,collector));
content=content.replace('var setting = BundleCollectorSettingData.Setting;','GameResourceAddressRule.Reset();\n            GameResourceAddressRule.WriteAliases();\n            var setting = BundleCollectorSettingData.Setting;');
content=content.replaceAll('AddressRuleName = nameof(AddressByFileName)','AddressRuleName = nameof(GameResourceAddressRule)');
content=content.replace('SynchronizeCollectors(group);\n            BundleCollectorSettingData.ModifyPackage(package);','SynchronizeCollectors(group);\n            foreach (var collector in group.Collectors) collector.AddressRuleName = nameof(GameResourceAddressRule);\n            BundleCollectorSettingData.ModifyPackage(package);');
put(collector,content);
// The old demo launcher/validation references business APIs; move them out of the AOT bootstrap.
for(const name of ['MigrationSmokeProbe.cs','MigrationSmokeProbe.cs.meta']) {
 const old='Assets/FrameWork/Tool/Bootstrap/'+name;if(fs.existsSync(path.join(dst,old))){put('Assets/FrameWork/Scripts/Migration/'+name,fs.readFileSync(path.join(dst,old)));remove(old);}
}
const legacyLauncher='Assets/FrameWork/Tool/Tool/Launcher.cs';
if(fs.existsSync(path.join(dst,legacyLauncher)))for(const suffix of ['','.meta']) {if(fs.existsSync(path.join(dst,legacyLauncher+suffix)))put('Assets/FrameWork/Tool/Bootstrap/Launcher.cs'+suffix,fs.readFileSync(path.join(dst,legacyLauncher+suffix)));remove(legacyLauncher+suffix);}
const obfuz='ProjectSettings/Obfuz.asset';if(fs.existsSync(path.join(dst,obfuz)))put(obfuz,read(path.join(dst,obfuz)).replace('    - FrameWork.Runtime','    - FrameWork.Bootstrap'));
for(const suffix of ['','.meta']) {
  const old='Assets/Editor/FrameworkMigrationValidation.cs'+suffix;
  if(fs.existsSync(path.join(dst,old))){put('Assets/FrameWork/Editor/GameProjectValidation.cs'+suffix,fs.readFileSync(path.join(dst,old)));remove(old);}
}
const boot='Assets/FrameWork/Resources/FrameworkBootSettings.asset';if(fs.existsSync(path.join(dst,boot)))put(boot,read(path.join(dst,boot)).replaceAll('FrameWork.Runtime.dll','FrameWork.Bootstrap.dll'));
// Never copy prebuilt DLLs, game scenes, prefab scripts, remote endpoints, or release baselines.
const summary={date:'2026-10-28',writes:[...writes.keys()].map(f=>path.relative(dst,f)),removed:[...deletes].map(f=>path.relative(dst,f)),moved};
fs.writeFileSync(path.join(__dirname,'plan.json'),JSON.stringify(summary,null,2));
if(!process.argv.includes('--apply')){console.log(JSON.stringify({writes:writes.size,removed:deletes.size,moved:moved.length}));process.exit();}
const backup=path.join(dst,'FrameworkMigrationBackup','Sync-20261028-'+Date.now());
for(const f of new Set([...writes.keys(),...deletes]))if(fs.existsSync(f)){const out=path.join(backup,path.relative(dst,f));fs.mkdirSync(path.dirname(out),{recursive:true});fs.copyFileSync(f,out);}
fs.mkdirSync(backup,{recursive:true});fs.writeFileSync(path.join(backup,'plan.json'),JSON.stringify(summary,null,2));
for(const f of deletes)fs.unlinkSync(f);
for(const [f,data] of writes){fs.mkdirSync(path.dirname(f),{recursive:true});fs.writeFileSync(f,data);}
console.log(JSON.stringify({backup,writes:writes.size,removed:deletes.size}));
