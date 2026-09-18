'use strict';
const $=(s,root=document)=>root.querySelector(s), $$=(s,root=document)=>Array.from(root.querySelectorAll(s));
const worldParams=new URLSearchParams(location.search);
const presets={
 quiet:{number:'B1',name:'克制描边',description:'只让原有边框变色，不增加第二圈线。悬停时灰框柔和提亮，选中时原框变绿，按下轻压 1px。',specs:[['悬停','140ms · 原边框变色'],['按下','65ms · 内容下沉 1px'],['释放','150ms · 平滑复位'],['记录','220ms · 展开 / 收起'],['窗口','180ms · 淡入 + 5px 位移']]},
 mechanical:{number:'B2',name:'机械按键',description:'强调实体按键手感：悬停轻抬，按压下沉，释放分三段归位。点击区域保持原位。',specs:[['悬停','90ms · 文字抬高 1px'],['按下','45ms · 下沉 2px'],['释放','160ms · 分段回弹'],['记录','210ms · 利落展开'],['窗口','170ms · 短距离进入']]},
 signal:{number:'B3',name:'信号标记',description:'反馈集中在边缘：角标展开、短线划过，时间轴亮起。记录选中用侧线表示，避免整块闪白。',specs:[['悬停','170ms · 角标 + 短线'],['按下','55ms · 角标收紧'],['释放','240ms · 短线回应'],['记录','260ms · 侧线 + 信息展开'],['窗口','210ms · 轻淡入']]}
};
let currentPreset='quiet', currentPage='saves', selected='p1', filter='all', version=0, pageVersion=0, busy=false, loadTimer, toastTimer, demoRunning=false;
let records=[
 {id:'p1',day:14,time:'14:30',place:'驾驶舱',icon:'驾驶室',kind:'manual',saved:'09/18 14:30',play:'3小时42分'},
 {id:'p2',day:14,time:'14:20',place:'动力舱',icon:'动力舱',kind:'auto',saved:'09/18 14:20',play:'3小时32分'},
 {id:'p3',day:14,time:'12:05',place:'维生舱',icon:'维生舱',kind:'auto',saved:'09/18 13:45',play:'3小时06分'},
 {id:'p4',day:13,time:'23:10',place:'驾驶舱',icon:'驾驶室',kind:'manual',saved:'09/17 21:10',play:'2小时50分'},
 {id:'p5',day:13,time:'18:40',place:'飞船外壳',icon:'飞船外壳',kind:'auto',saved:'09/17 20:00',play:'2小时18分'}
];
if(worldParams.has('worldName')){
 const day=Number(worldParams.get('day'))||14;
 const [hour,minute]=(worldParams.get('time')||'14:30').split(':').map(Number);
 const latest=(day-1)*1440+hour*60+minute;
 const count=Math.max(1,Math.min(5,Number(worldParams.get('count'))||5));
 records=records.slice(0,count).map((r,i)=>{
  const total=Math.max(0,latest-i*45), m=total%1440;
  return {...r,day:Math.floor(total/1440)+1,time:String(Math.floor(m/60)).padStart(2,'0')+':'+String(m%60).padStart(2,'0'),...(i===0?{place:worldParams.get('place')||r.place,icon:worldParams.get('place')||r.icon,play:worldParams.get('play')||r.play}:{})};
 });
 if(worldParams.get('hardcore')==='true')records=records.slice(0,1);
}
const kinds={manual:'手动',auto:'自动'};
let draft={name:'麦麦正在研究矿石释氧机',hardcore:false,tutorial:true};
let settings={auto:true,interval:10,count:10,sound:70,muted:false,speed:1}, category='存档';
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function button(label,attrs='',classes=''){return `<button class="ui-btn ${classes}" ${attrs}><span class="face">${label}</span></button>`}
function toast(text){clearTimeout(toastTimer);$('#toast').textContent=text;$('#toast').classList.add('visible');toastTimer=setTimeout(()=>$('#toast').classList.remove('visible'),2100)}
function setPreset(key){stopDemo();cancelBusy();currentPreset=key;document.body.dataset.motion=key;const p=presets[key];$$('[data-preset]').forEach(b=>b.setAttribute('aria-pressed',b.dataset.preset===key));$('#scheme-number').textContent=p.number;$('#scheme-name').textContent=p.name;$('#scheme-description').textContent=p.description;$('#specs').innerHTML=p.specs.map(([k,v])=>`<dt>${k}</dt><dd>${v}</dd>`).join('');}
function stopDemo(){version++;demoRunning=false;$('#play-demo').textContent='播放一轮反馈';$$('#game .demo-hover,#game .demo-press,#game .released').forEach(e=>e.classList.remove('demo-hover','demo-press','released'));$('#demo-step').textContent='试试：悬停按钮 → 按住 → 选择另一条记录 → 读取。Tab / Enter / Esc 也可以操作。';}
function cancelBusy(){clearTimeout(loadTimer);busy=false;$$('#game [data-was-enabled]').forEach(e=>{e.disabled=false;e.removeAttribute('data-was-enabled')});const b=$('#read');if(b){b.removeAttribute('aria-busy');$('.face',b).textContent='读取此记录';$('.load-line',b)?.remove();}}
function setBusy(){busy=true;$$('#game button').filter(b=>!b.disabled).forEach(b=>{b.dataset.wasEnabled='1';b.disabled=true});const b=$('#read');b.setAttribute('aria-busy','true');$('.face',b).textContent='读取中…';b.insertAdjacentHTML('beforeend','<i class="load-line"></i>');}
function applySelection(id){if(busy)return;selected=id;$$('.timeline-row').forEach(row=>{const active=row.dataset.id===id;row.classList.toggle('selected',active);$('.record',row).classList.toggle('selected',active);$('.record-select',row).setAttribute('aria-expanded',String(active));$('.record-details',row).inert=!active;$('.record-details',row).setAttribute('aria-hidden',String(!active));});const r=records.find(r=>r.id===selected);$('#selection-summary').textContent=r?`已选择：第${r.day}天 ${r.time} · ${r.place}`:'此分类没有保存记录';$('#read').disabled=!r;$('#delete').disabled=!r;}
function applyFilter(kind){if(busy)return;filter=kind;$$('[data-filter]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.filter===kind)));const visible=records.filter(r=>kind==='all'||r.kind===kind);let lastDay=null;$$('.row-clip').forEach(clip=>{const r=records.find(r=>r.id===clip.dataset.row);const show=visible.includes(r);clip.classList.toggle('filtered',!show);clip.inert=!show;clip.setAttribute('aria-hidden',String(!show));if(show){$('.day',clip).textContent=lastDay!==r.day?`第${r.day}天`:'';$('.season',clip).textContent=lastDay!==r.day?'温和季':'';lastDay=r.day;}});applySelection(visible.some(r=>r.id===selected)?selected:visible[0]?.id||null);$('#empty').hidden=visible.length>0;}
function recordMarkup(r){return `<div class="row-clip" data-row="${r.id}"><div class="timeline-row" data-id="${r.id}"><div class="time-rail"><span class="season"></span><span class="day"></span><span class="time">${r.time}</span></div><article class="record"><button class="record-select" data-record="${r.id}" aria-expanded="false" aria-controls="detail-${r.id}" aria-label="${r.place} 第${r.day}天 ${r.time} ${kinds[r.kind]}保存"><img class="place-icon" src="assets/${r.icon}.png" alt=""><span class="record-title">${r.place}</span><span class="badge">${kinds[r.kind]}</span><span class="chevron" aria-hidden="true">›</span></button><div id="detail-${r.id}" class="record-details"><div class="details-inner"><div class="details-copy"><p>温和季 · 第${r.day}天 ${r.time}</p><p class="secondary">${r.saved} 保存 · 累计游玩 ${r.play}</p>${r.kind==='auto'?button('保留为手动记录',`data-pin="${r.id}"`,'save-pin'):''}</div></div></div></article></div></div>`;}
function saves(){return `<div class="run-line"><h2 class="run-name">${esc(worldParams.get("worldName")||"麦麦听说废铁刀和白爆矿在谈恋爱")}</h2><div class="run-meta"><span class="badge">${worldParams.get("hardcore")==="true"?"硬核模式":"普通模式"}</span>${button('世界线管理','id="manage" aria-expanded="false"')}</div></div><nav class="filters" aria-label="记录分类">${[['all','全部'],['auto','自动'],['manual','手动']].map(([k,v])=>button(v,`data-filter="${k}" aria-pressed="false"`)).join('')}</nav><div class="records" aria-label="保存时间点">${records.map(recordMarkup).join('')}<p id="empty" class="setting-empty" hidden>此分类没有保存记录。</p></div><footer class="window-footer"><span class="selection-summary" id="selection-summary"></span><div class="actions">${button('删除记录','id="delete"','danger')}${button('返回','id="back"')}${button('读取此记录','id="read"','primary')}</div></footer>`;}
function toggle(label,id,value){return button(`<span class="toggle-track" aria-hidden="true"><i></i></span><span class="toggle-text">${label}</span>`,`id="${id}" aria-pressed="${value}"`,'toggle')}
function creation(){return `<h2 class="page-heading">麦麦的新一段旅途</h2><p class="page-hint">为这段故事取一个名字</p><label class="field-label" for="run-name">世界线名称</label><div class="name-row"><input id="run-name" class="name-field" maxlength="30" value="${esc(draft.name)}" autocomplete="off">${button('随机','id="random"')}</div><div class="choices"><button class="choice" data-mode="ordinary" aria-pressed="${!draft.hardcore}"><span class="choice-label">普通模式</span><span class="hint">保留多个保存点<br>死亡后可以回档</span></button><button class="choice" data-mode="hardcore" aria-pressed="${draft.hardcore}"><span class="choice-label">硬核模式</span><span class="hint">仅保留最新进度<br>死亡后删除世界线</span></button></div><div class="inline-setting"><span>新手教程</span>${toggle(draft.tutorial?'开启':'跳过','tutorial',draft.tutorial)}</div><p class="page-hint" id="mode-hint">${draft.hardcore?'硬核模式退出前必须保存，死亡后世界线将被删除。':'普通模式允许读取之前的保存记录。'}</p><footer class="window-footer"><span class="selection-summary"></span><div class="actions">${button('返回','id="back"')}${button('开始游戏',`id="start" ${draft.name.trim()?'':'disabled'}`,'primary')}</div></footer><div class="status-message" id="form-status" aria-live="polite"></div>`;}
function stepper(id,value,unit){return `<div class="stepper">${button('−',`data-step="${id}" data-delta="-1" aria-label="减少${id==='interval'?'间隔':'记录数'}"`)}<output id="value-${id}">${value} ${unit}</output>${button('＋',`data-step="${id}" data-delta="1" aria-label="增加${id==='interval'?'间隔':'记录数'}"`)}</div>`;}
function settingsBody(){if(category==='存档')return `<div class="setting-row"><span>自动保存</span>${toggle(settings.auto?'开启':'关闭','autosave',settings.auto)}</div><div class="setting-row auto-setting" aria-disabled="${!settings.auto}"><span>保存间隔<span class="sub">按实际游玩时间计算</span></span>${stepper('interval',settings.interval,'分钟')}</div><div class="setting-row auto-setting" aria-disabled="${!settings.auto}"><span>自动记录上限<span class="sub">手动记录永久保留</span></span>${stepper('count',settings.count,'条')}</div>`;
 if(category==='声音')return `<div class="setting-row"><label for="volume">总音量</label><div class="slider-row"><input id="volume" type="range" min="0" max="100" value="${settings.sound}"><output id="volume-value">${settings.sound}%</output></div></div><div class="setting-row"><span>静音</span>${toggle(settings.muted?'开启':'关闭','mute',settings.muted)}</div>`;
 if(category==='阅读')return `<div class="setting-row"><span>对话速度</span>${button('× '+settings.speed,'id="speed"')}</div><p class="page-hint">点击切换对话速度。</p>`;
 return `<p class="setting-empty">这一页演示显示设置的确认反馈。</p>${button('预览显示设置确认','id="display-preview"')}`;}
function settingsPage(){return `<h2 class="page-heading">${category}</h2><nav class="settings-tabs" aria-label="设置分类">${['声音','存档','阅读','显示'].map(c=>button(c,`data-category="${c}" aria-pressed="${category===c}"`)).join('')}</nav><div id="settings-body">${settingsBody()}</div><footer class="window-footer"><span class="selection-summary" id="settings-status" aria-live="polite"></span><div class="actions">${button('恢复本页默认','id="reset"')}${button('完成','id="done"','primary')}</div></footer>`;}
function renderPage(){const host=$('#page-host');host.innerHTML=currentPage==='saves'?saves():currentPage==='create'?creation():settingsPage();$('#window-title').textContent=currentPage==='saves'?'航行日志':currentPage==='create'?'创建世界线':'设置';$$('[data-page]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.page===currentPage)));host.classList.remove('page-enter');void host.offsetWidth;host.classList.add('page-enter');if(currentPage==='saves')applyFilter(filter);if(currentPage==='settings')refreshAutoControls();}
async function switchPage(page,stop=true){closeVersion++;if(stop)stopDemo();cancelBusy();closeMenu();const token=++pageVersion;$('#shell').hidden=false;$('#shell').classList.remove('closing');$('#reopen').hidden=true;$('#page-host').classList.add('leaving');await new Promise(r=>setTimeout(r,document.body.classList.contains('reduce')?0:80));if(token!==pageVersion)return;currentPage=page;renderPage();$('#page-host').classList.remove('leaving');}
function refreshAutoControls(){$$('.auto-setting').forEach(row=>{row.setAttribute('aria-disabled',String(!settings.auto));$$('button',row).forEach(b=>b.disabled=!settings.auto)});$$('[data-step]').forEach(b=>{const v=settings[b.dataset.step],max=b.dataset.step==='interval'?60:50;b.disabled=!settings.auto||(b.dataset.delta==='-1'?v<=1:v>=max)});}
function updated(text=''){const status=$('#settings-status');if(status){status.textContent=text;status.style.color='var(--accent)';}}
function readRecord(){if(busy||!selected)return;const r=records.find(r=>r.id===selected);setBusy();loadTimer=setTimeout(()=>{cancelBusy();toast(`演示：已读取第${r.day}天 ${r.time} 的记录`);},850);}
let onConfirm=null, previousFocus=null;
function confirmAction(title,copy,action,yes='确认删除',danger=true){previousFocus=document.activeElement;$('#confirm-title').textContent=title;$('#confirm-copy').textContent=copy;$('#confirm-yes .face').textContent=yes;$('#confirm-yes').classList.toggle('danger',danger);onConfirm=action;$('#confirm').returnValue='';$('#confirm').showModal();}
$('#confirm').addEventListener('close',()=>{const action=onConfirm;onConfirm=null;if($('#confirm').returnValue==='yes')action?.();if(previousFocus?.isConnected)previousFocus.focus();});
function closeMenu(){$('.manage-menu')?.remove();$('#manage')?.setAttribute('aria-expanded','false');}
function manage(){if($('.manage-menu')){closeMenu();return;}$('#manage').setAttribute('aria-expanded','true');$('#shell').insertAdjacentHTML('beforeend',`<div class="manage-menu" aria-label="世界线管理">${button('复制世界线','data-manage="复制"')}${button('分享世界线','data-manage="分享"')}${button('修改名称','data-manage="改名"')}</div>`);}
$('#game').addEventListener('click',e=>{const b=e.target.closest('button');if(!b||b.disabled)return;
 if(b.id==='back'&&worldParams.has('worldName')){location.href='../worldlines/';return;}
 if(b.id==='window-close'||b.id==='back'){closeWindow();return;}
 if(b.dataset.record){applySelection(b.dataset.record);return;}
 if(b.dataset.filter){applyFilter(b.dataset.filter);return;}
 if(b.id==='read'){readRecord();return;}
 if(b.id==='manage'){manage();return;}
 if(b.dataset.manage){const t=b.dataset.manage;closeMenu();toast(t==='改名'?'演示：打开世界线重命名':'演示：'+t+'世界线');return;}
 if(b.id==='delete'){const r=records.find(r=>r.id===selected);if(!r)return;confirmAction('删除这个保存点？',`第${r.day}天 ${r.time} · ${r.place}。其他保存点不受影响。这里仅删除演示数据。`,()=>{records=records.filter(v=>v.id!==r.id);renderPage();toast('演示记录已删除');});return;}
 if(b.dataset.pin){const r=records.find(r=>r.id===b.dataset.pin);r.kind='manual';renderPage();toast('已保留为手动记录（演示）');return;}
 if(b.id==='random'){const names=['麦麦把白爆矿塞进了冰箱','麦麦听说燃素和废铁刀在谈恋爱','麦麦正在研究矿石释氧机','麦麦残忍地杀害了白爆矿'];draft.name=names[(names.indexOf(draft.name)+1)%names.length];const input=$('#run-name');input.value=draft.name;input.classList.remove('generated');void input.offsetWidth;input.classList.add('generated');$('#start').disabled=false;$('#form-status').textContent='已生成一个新的世界线名称';return;}
 if(b.dataset.mode){draft.hardcore=b.dataset.mode==='hardcore';$$('[data-mode]').forEach(v=>v.setAttribute('aria-pressed',String((v.dataset.mode==='hardcore')===draft.hardcore)));$('#mode-hint').textContent=draft.hardcore?'硬核模式退出前必须保存，死亡后世界线将被删除。':'普通模式允许读取之前的保存记录。';return;}
 if(b.id==='tutorial'){draft.tutorial=!draft.tutorial;b.setAttribute('aria-pressed',String(draft.tutorial));$('.toggle-text',b).textContent=draft.tutorial?'开启':'跳过';return;}
 if(b.id==='start'){toast('演示：创建'+(draft.hardcore?'硬核':'普通')+'世界线');return;}
 if(b.dataset.category){category=b.dataset.category;renderPage();return;}
 if(b.id==='autosave'){settings.auto=!settings.auto;b.setAttribute('aria-pressed',String(settings.auto));$('.toggle-text',b).textContent=settings.auto?'开启':'关闭';refreshAutoControls();updated();return;}
 if(b.dataset.step){const key=b.dataset.step;settings[key]=Math.max(1,Math.min(key==='interval'?60:50,settings[key]+Number(b.dataset.delta)));const output=$('#value-'+key);output.textContent=settings[key]+(key==='interval'?' 分钟':' 条');output.classList.remove('changed');void output.offsetWidth;output.classList.add('changed');refreshAutoControls();updated();return;}
 if(b.id==='mute'){settings.muted=!settings.muted;b.setAttribute('aria-pressed',String(settings.muted));$('.toggle-text',b).textContent=settings.muted?'开启':'关闭';updated();return;}
 if(b.id==='speed'){settings.speed=settings.speed===3?1:settings.speed+1;$('.face',b).textContent='× '+settings.speed;updated();return;}
 if(b.id==='display-preview'){confirmAction('保留这组显示设置？','这是确认窗口的动效预览，不会更改电脑的分辨率。',()=>toast('演示：已保留显示设置'),'保留设置',false);return;}
 if(b.id==='reset'){confirmAction('恢复本页默认值？','只恢复当前分类的演示设置。',()=>{if(category==='存档'){settings.auto=true;settings.interval=10;settings.count=10;}if(category==='声音'){settings.sound=70;settings.muted=false;}if(category==='阅读')settings.speed=1;renderPage();updated('已恢复本页默认值');},'恢复默认',false);return;}
 if(b.id==='done')switchPage('saves');
});
$('#game').addEventListener('input',e=>{if(e.target.id==='run-name'){draft.name=e.target.value;$('#start').disabled=!draft.name.trim();$('#form-status').textContent=draft.name.trim()?'':'请输入世界线名称';}if(e.target.id==='volume'){settings.sound=Number(e.target.value);$('#volume-value').textContent=settings.sound+'%';updated();}});
document.addEventListener('pointerup',e=>{const b=e.target.closest('.ui-btn');if(!b||b.disabled||b.closest('.notes'))return;b.classList.remove('released');void b.offsetWidth;b.classList.add('released');setTimeout(()=>b.classList.remove('released'),260);});
let closeVersion=0;
function closeWindow(){stopDemo();cancelBusy();closeMenu();const id=++closeVersion;$('#shell').classList.add('closing');setTimeout(()=>{if(id===closeVersion){$('#shell').hidden=true;$('#reopen').hidden=false;$('#reopen').focus();}},120);}
$('#reopen').onclick=()=>{closeVersion++;$('#shell').hidden=false;$('#shell').classList.remove('closing');$('#shell').classList.add('opening');$('#reopen').hidden=true;setTimeout(()=>$('#shell').classList.remove('opening'),230);};
$$('[data-preset]').forEach(b=>b.onclick=()=>setPreset(b.dataset.preset));$$('[data-page]').forEach(b=>b.onclick=()=>switchPage(b.dataset.page));
$('#reduce').checked=matchMedia('(prefers-reduced-motion: reduce)').matches;
$('#reduce').onchange=e=>{document.body.classList.toggle('reduce',e.target.checked);document.body.classList.toggle('force-motion',!e.target.checked);};
document.addEventListener('keydown',e=>{if(e.key==='Escape'&&!$('#confirm').open){if($('.manage-menu'))closeMenu();else if(!$('#shell').hidden)closeWindow();}});
document.addEventListener('click',e=>{if(!e.target.closest('#manage,.manage-menu'))closeMenu();});
async function wait(ms,id){await new Promise(r=>setTimeout(r,ms));if(id!==version)throw new Error('cancelled');}
$('#play-demo').onclick=async()=>{if(demoRunning){stopDemo();cancelBusy();return;}stopDemo();cancelBusy();const id=version;demoRunning=true;$('#play-demo').textContent='停止演示';try{
 await switchPage('saves',false);await wait(200,id);applyFilter('all');const read=$('#read');$('#demo-step').textContent='1 / 5 · 悬停：观察边框、角标或按键的轻微变化';read.classList.add('demo-hover');await wait(900,id);
 $('#demo-step').textContent='2 / 5 · 按住：反馈发生在视觉层，点击区域不移动';read.classList.add('demo-press');await wait(600,id);
 $('#demo-step').textContent='3 / 5 · 释放：归位或短线回应，不使用整块反色';read.classList.remove('demo-press');read.classList.add('released');await wait(700,id);read.classList.remove('demo-hover','released');
 $('#demo-step').textContent='4 / 5 · 选择记录：时间轴、边框与展开内容一起响应';const r=records.find(r=>r.id!==selected);if(r)applySelection(r.id);await wait(1100,id);
 $('#demo-step').textContent='5 / 5 · 读取：进入处理中状态，重复点击被阻止';readRecord();await wait(1500,id);
 if(id===version){demoRunning=false;$('#play-demo').textContent='播放一轮反馈';$('#demo-step').textContent='演示结束。可切换另一套方案，重复同样操作进行比较。';}
 }catch(e){if(e.message!=='cancelled')console.error(e);}};
setPreset('quiet');renderPage();
