const fs=require('fs'),path=require('path');
const root='C:/Svn/Game';
const backup=path.join(root,'FrameworkMigrationBackup/Sync-20261028-1793191012628');
for(const suffix of ['','.meta']) {
  const rel='Assets/Editor/FrameworkMigrationValidation.cs'+suffix;
  const old=path.join(root,rel), next=path.join(root,'Assets/FrameWork/Editor/GameProjectValidation.cs'+suffix);
  if(!fs.existsSync(old))continue;
  const saved=path.join(backup,rel);fs.mkdirSync(path.dirname(saved),{recursive:true});fs.copyFileSync(old,saved);
  fs.copyFileSync(old,next);fs.unlinkSync(old);
}
