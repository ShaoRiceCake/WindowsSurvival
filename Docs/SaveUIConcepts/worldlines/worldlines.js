'use strict';
const $=s=>document.querySelector(s), $$=s=>[...document.querySelectorAll(s)];
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const asset='../motion/assets/';
const initial=[
 {id:1,name:'麦麦听说废铁刀和白爆矿在谈恋爱',place:'驾驶室',day:14,time:'14:30',played:'3小时42分',count:5,last:'今天',clock:'15:51',hard:false},
 {id:2,name:'麦麦把白爆矿塞进了冰箱',place:'维生舱',day:8,time:'09:10',played:'1小时26分',count:3,last:'昨天',clock:'21:08',hard:false},
 {id:3,name:'麦麦带着燃素寻找氧气罐',place:'动力舱',day:21,time:'22:45',played:'6小时18分',count:1,last:'09/16',clock:'18:34',hard:true},
 {id:4,name:'麦麦正在研究矿石释氧机',place:'飞船外壳',day:3,time:'11:20',played:'38分钟',count:4,last:'09/14',clock:'20:17',hard:false}
];
let worlds=structuredClone(initial), selected=1, demo='normal', nextId=100, toastTimer, onConfirm=null, restoreFocus=null;
const current=()=>worlds.find(w=>w.id===selected);
const button=(label,attrs='',cls='')=>`<button class="button ${cls}" ${attrs}><span>${label}</span></button>`;

function notify(text){clearTimeout(toastTimer);$('#toast').textContent=text;$('#toast').hidden=false;toastTimer=setTimeout(()=>$('#toast').hidden=true,2800);}
function renderList(){
 $('#count').textContent=worlds.length;
 $('#world-list').innerHTML=worlds.length?worlds.map(w=>`<button class="world" data-world="${w.id}" aria-pressed="${selected===w.id}"><strong>${esc(w.name)}</strong><span class="world-meta"><span>${w.hard?'硬核':'普通'} · 第${w.day}天 ${w.time}</span><span class="stamp">${w.last}</span></span></button>`).join(''):'<p class="list-empty">还没有世界线</p>';
}
function renderDetail(){
 const w=current();const host=$('#details');
 if(!w){host.innerHTML=`<div class="empty-state"><img src="${asset}驾驶室.png" alt=""><h2>麦麦根本不存在</h2><p>创建一条世界线，<br>或导入一份已有的故事。</p>${button('+ 新建世界线','data-action="create"','primary')}</div>`;return;}
 host.innerHTML=`<div class="detail-body detail-enter"><div class="world-heading"><span class="badge ${w.hard?'hard':''}">${w.hard?'硬核模式':'普通模式'}</span></div><h2 class="detail-title">${esc(w.name)}</h2><div class="location"><img class="place-icon" src="${asset}${w.place}.png" alt=""><div><strong>${w.place}</strong><p>温和季 · 第${w.day}天 ${w.time}</p></div></div><dl class="metrics"><div><dt>累计游玩</dt><dd>${w.played}</dd></div><div><dt>保存记录</dt><dd>${w.count} 个</dd></div><div><dt>最近游玩</dt><dd>${w.last}<small>${w.clock}</small></dd></div></dl>${w.hard?'<p class="hard-note">只保留最新进度，死亡后删除世界线。</p>':''}</div><footer class="actions"><div class="manage-wrap">${button('管理世界线 <i class="caret" aria-hidden="true"></i>','id="manage" aria-expanded="false" aria-controls="manage-menu"')}<div class="menu" id="manage-menu" hidden>${button('修改名称','data-action="rename"')}${button('复制世界线','data-action="copy"')}${button('分享世界线','data-action="share"')}<hr>${button('删除世界线','data-action="delete"','danger')}</div></div><button class="button primary load" data-action="load"><span>载入游戏 <b class="arrow" aria-hidden="true">→</b></span></button></footer>`;
}
function render(){renderList();renderDetail();}
function closeMenu(focus=false){const m=$('#manage-menu');if(!m)return;m.hidden=true;$('#manage').setAttribute('aria-expanded','false');if(focus)$('#manage').focus();}
function dialog(title,html,confirm,callback,{danger=false,input=false}={}){
 closeMenu();restoreFocus=document.activeElement;$('#dialog-title').textContent=title;$('#dialog-content').innerHTML=html;$('#confirm').innerHTML=`<span>${confirm}</span>`;$('#confirm').className='button '+(danger?'danger':'primary');$('#confirm').disabled=false;onConfirm=callback;$('#dialog').showModal();
 if(input){const field=$('#dialog-content input');field.focus();field.select();field.addEventListener('input',()=>$('#confirm').disabled=!field.value.trim());}else $('#cancel').focus();
}
function endDialog(){onConfirm=null;$('#dialog').close();if(restoreFocus?.isConnected)restoreFocus.focus();}
function create(){dialog('新建世界线','<label for="name">世界线名称</label><input id="name" maxlength="30" value="麦麦把燃素塞进了冰箱"><p style="margin-top:16px">演示将创建一条普通世界线。</p>','创建',()=>{const w={...initial[0],id:nextId++,name:$('#name').value.trim(),day:1,time:'00:00',count:1,played:'0分钟',last:'今天',clock:'现在'};worlds.unshift(w);selected=w.id;render();notify('已创建世界线（演示）');},{input:true});}
function act(action){
 if(action==='create'){create();return;}const w=current();if(!w)return;
 if(action==='rename')dialog('修改名称',`<label for="name">世界线名称</label><input id="name" maxlength="30" value="${esc(w.name)}">`,'保存名称',()=>{w.name=$('#name').value.trim();render();notify('名称已更新');},{input:true});
 if(action==='copy')dialog('复制世界线',`<h2>${esc(w.name)}</h2><p>${w.hard?'复制后的世界线使用普通模式。\n':''}副本拥有独立的保存记录。</p>`,'创建副本',()=>{const copy={...w,id:nextId++,name:w.name+' · 副本',hard:false,last:'今天',clock:'现在'};worlds.unshift(copy);selected=copy.id;render();notify('已创建独立副本');});
 if(action==='share')dialog('分享世界线',`<h2>${esc(w.name)}</h2><p>${w.hard?'分享副本将使用普通模式。\n':''}${w.count} 个保存点 · .wssave 文件</p>`,'导出文件',()=>notify('演示：此处导出世界线分享文件'));
 if(action==='delete')dialog('删除世界线',`<h2>${esc(w.name)}</h2><p>同时删除这条世界线的 ${w.count} 个保存点。\n删除后无法恢复。</p>`,'确认删除',()=>{worlds=worlds.filter(v=>v.id!==w.id);selected=worlds[0]?.id;render();notify('已删除这条演示世界线');},{danger:true});
 if(action==='load'){const query=new URLSearchParams({worldName:w.name,worldId:String(w.id),hardcore:String(w.hard),place:w.place,day:String(w.day),time:w.time,play:w.played,count:String(w.count)});location.href='../motion/?'+query.toString();}
}
document.addEventListener('click',e=>{
 const b=e.target.closest('button');
 if(b?.dataset.demo){demo=b.dataset.demo;worlds=demo==='empty'?[]:structuredClone(initial);if(demo==='many')for(let i=0;i<18;i++)worlds.push({...initial[i%4],id:nextId++,name:['麦麦弄丢了塑料袋','麦麦用氧烛换来了废铁刀','麦麦决定相信燃素'][i%3],last:'09/'+String(13-i%12).padStart(2,'0')});selected=demo==='hardcore'?3:worlds[0]?.id;$$('[data-demo]').forEach(v=>v.setAttribute('aria-pressed',String(v===b)));render();return;}
 if(b?.dataset.world){selected=Number(b.dataset.world);renderList();renderDetail();$(`[data-world="${selected}"]`).focus({preventScroll:true});return;}
 if(b?.id==='manage'){const m=$('#manage-menu');m.hidden=!m.hidden;b.setAttribute('aria-expanded',String(!m.hidden));return;}
 if(b?.dataset.action){act(b.dataset.action);return;}

 if(!e.target.closest('.manage-wrap'))closeMenu();
});
$('#create').onclick=create;$('#import').onclick=()=>dialog('导入世界线','<p>选择 .wssave 文件，导入为一条独立世界线。</p>'+button('选择文件','type="button" id="choose-file"'),'返回',()=>{});
$('#dialog-content').addEventListener('click',e=>{if(e.target.closest('#choose-file'))$('#file').click();});
$('#file').addEventListener('change',()=>notify($('#file').files.length?'已选择文件；设计稿不会读取游戏存档':'未选择文件'));
$('#cancel').onclick=endDialog;$('#dialog-close').onclick=endDialog;
$('#dialog').addEventListener('cancel',e=>{e.preventDefault();endDialog();});
$('#dialog-form').addEventListener('submit',e=>{e.preventDefault();const fn=onConfirm;if(!fn)return;fn();endDialog();});
$('#close').onclick=()=>{closeMenu();$('.stage>.window').hidden=true;$('.closed').hidden=false;};
$('#reopen').onclick=()=>{$('.stage>.window').hidden=false;$('.closed').hidden=true;render();};
document.addEventListener('keydown',e=>{if(e.key==='Escape'&&!$('#dialog').open){if($('#manage-menu')&&!$('#manage-menu').hidden){e.preventDefault();closeMenu(true);}else $('#close').click();}});
render();
