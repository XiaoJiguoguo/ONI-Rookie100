const $ = s => document.querySelector(s);
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let catalog, chapterIndex = 0, taskId = 'q01_dining', activeTab = 'overview', showAll = false;
const completed = new Set(['q01','q01_bedroom']);
let diningResearch = false, diningBuilt = false;
const chapters = ['安顿生活','稳定生存','基础设施','探索控温','持续发展','走向太空'];
function current() { return catalog.tasks.find(t => t.id === taskId); }
function toast(message) { $('#toast span').textContent = message; $('#toast').hidden = false; }
function modal(title, html) { $('#modal-title').textContent = title; $('#modal-content').innerHTML = html; $('#modal').showModal(); }
function scene(index) {
 const palettes = [['#d8d7bc','#9c9370','#60756b'],['#d2e0c6','#82a47a','#5b8b98'],['#d4deea','#8196aa','#566a86'],['#e3d4c5','#a98970','#918773'],['#dbd5e5','#968ba5','#646578'],['#c9cedf','#7c85a8','#666f91']];
 const [bg, rock, accent] = palettes[index];
 const tile = (x,y,w=46,h=18) => `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="2" fill="${rock}" stroke="#454a4f" stroke-width="2"/><path d="M${x+4} ${y+4}h${w-8}" stroke="#fff" opacity=".3"/>`;
 const room = `<path d="M76 98V28H467V98M268 28V98" fill="none" stroke="${rock}" stroke-width="14"/><path d="M78 24H467" stroke="#fff" opacity=".3" stroke-width="2"/>${tile(60,98,425,17)}<rect x="100" y="69" width="125" height="20" rx="5" fill="#6f8e8a" stroke="#43484a" stroke-width="3"/><rect x="105" y="62" width="33" height="17" rx="4" fill="#e9dec5" stroke="#43484a" stroke-width="2"/><path d="M101 86V96M224 86V96" stroke="#43484a" stroke-width="4"/><rect x="316" y="65" width="89" height="10" rx="2" fill="#b29675" stroke="#43484a" stroke-width="2"/><path d="M329 76V97M391 76V97" stroke="#43484a" stroke-width="4"/><path d="M296 83h20v14M410 83h20v14" fill="none" stroke="#748b78" stroke-width="6"/>`;
 const farm = `${tile(65,98,420)}<path d="M80 87h107v11H80z" fill="#9d7c5e" stroke="#454a4f" stroke-width="2"/><path d="M108 89V55M150 89V48" stroke="#4e7950" stroke-width="5"/><ellipse cx="98" cy="64" rx="13" ry="7" fill="#789861" transform="rotate(25 98 64)"/><ellipse cx="158" cy="59" rx="16" ry="8" fill="#789861" transform="rotate(-25 158 59)"/><rect x="225" y="40" width="67" height="58" rx="6" fill="#8d9d93" stroke="#424b4f" stroke-width="3"/><circle cx="258" cy="68" r="17" fill="#d0dccf" stroke="#526565" stroke-width="3"/><path d="M340 78q55 -17 113 0v20H340z" fill="#81aeb4" stroke="#586f79" stroke-width="2"/>`;
 const machines = `${tile(60,99,430)}<rect x="86" y="56" width="88" height="42" rx="3" fill="#85959b" stroke="#424751" stroke-width="3"/><circle cx="128" cy="74" r="13" fill="#c6b17d" stroke="#424751" stroke-width="3"/><path d="M180 86h50V46h70v38h53" fill="none" stroke="#b0a274" stroke-width="7"/><rect x="244" y="34" width="43" height="24" fill="#869288" stroke="#424751" stroke-width="3"/><rect x="365" y="48" width="89" height="51" rx="3" fill="#8995a4" stroke="#424751" stroke-width="3"/><path d="M372 57h75M372 66h75M372 75h75" stroke="#515b69" stroke-width="3"/>`;
 const terrain = `${tile(60,99,155)}${tile(326,99,167)}<path d="M215 117l28 -22 15 -31 40 -12 28 65" fill="${rock}" stroke="#62605c" stroke-width="2"/><path d="M192 20v79M206 20v79M192 33h14M192 48h14M192 63h14M192 78h14M192 93h14" stroke="#697883" stroke-width="4"/><path d="M385 95V39h35v56" fill="#a5b1b0" stroke="#545966" stroke-width="3"/><circle cx="402" cy="54" r="8" fill="#9f7869"/>`;
 const rocket = `${tile(144,99,274)}<path d="M270 13q-24 16-24 40v42h48V53q0-24-24-40" fill="#ddd9ca" stroke="#46526a" stroke-width="3"/><circle cx="270" cy="47" r="11" fill="#86a7ba" stroke="#46526a" stroke-width="3"/><path d="M246 73l-15 22h15M294 73l15 22h-15" fill="#9b6e85" stroke="#46526a" stroke-width="3"/><path d="M92 32h6M95 29v6M404 25h8M408 21v8M444 66h6M447 63v6M147 58h6M150 55v6" stroke="#eee7ce" stroke-width="2"/>`;
 return `<svg viewBox="0 0 550 122" aria-label="章节主题场景" role="img"><rect width="550" height="122" fill="${bg}"/><path d="M0 22H550M0 47H550M0 72H550M0 97H550M25 0V122M50 0V122M500 0V122M525 0V122" stroke="${accent}" opacity=".12"/>${[room,farm,machines,terrain,machines,rocket][index]}<path d="M0 117H550" stroke="${accent}" stroke-width="10"/></svg><span class="scene-label">CHAPTER 0${index+1} / ${chapters[index]}</span>`;
}
function render() {
 const chapter = catalog.chapters[chapterIndex], task = current(), teaching = task.teaching;
 $('#chapters').innerHTML = chapters.map((title,i)=>`<button class="${i===chapterIndex?'active':''}" data-chapter="${i}" aria-current="${i===chapterIndex?'page':'false'}"><span class="number">${i+1}</span>${title}</button>`).join('');
 const tasks = catalog.tasks.filter(t=>t.chapterId===chapter.id);
 const done = tasks.filter(t=>t.track==='main' && completed.has(t.id)).length;
 $('#chapter-progress').textContent = `${done} / ${chapter.mainTaskIds.length}`;
 $('#chapter-title').textContent = chapter.title;
 $('#chapter-outcome').textContent = chapter.outcome;
 $('#chapter-fill').style.width = `${done/chapter.mainTaskIds.length*100}%`;
 $('#main-filter').classList.toggle('active',!showAll); $('#all-filter').classList.toggle('active',showAll);
 $('#tasks').innerHTML = tasks.filter(t=>showAll || t.track==='main').map(t=>`<button data-task="${esc(t.id)}" class="task-row ${t.id===taskId?'active':''}" aria-pressed="${t.id===taskId}"><span class="status-icon">${completed.has(t.id)?'✓':t.id===taskId?'▸':'◇'}</span><span class="row-text">${esc(t.title)}<small>${completed.has(t.id)?'已完成':t.id===taskId?'当前查看':t.track==='optional'?'按需选做':'待学习'}${t.implementation!=='existingRuntime'?' · 教学自查':''}</small></span></button>`).join('');
 $('#scene').innerHTML=scene(chapterIndex);
 $('#task-meta').textContent=`第${chapterIndex+1}章 · ${task.track==='main'?'主线任务':'选做路线'} · ${completed.has(taskId)?'已完成':'学习中'}`;
 $('#task-title').textContent=task.title; $('#task-purpose').textContent=teaching.purpose+'。';
 document.querySelectorAll('[data-tab]').forEach(b=>{b.classList.toggle('active',b.dataset.tab===activeTab);b.setAttribute('aria-selected',String(b.dataset.tab===activeTab));});
 const dining=taskId==='q01_dining';
 const next=dining ? (!diningResearch?'完成「食物制备」研究':!diningBuilt?'建造餐桌并围合餐厅':'检查原版房间识别') : teaching.steps[0];
 const explanation=dining ? (!diningResearch?'餐桌尚未解锁。先准备研究台、电源和泥土，再让复制人完成研究。':!diningBuilt?'餐桌已解锁。准备材料，保证施工和使用位置可达，再查看房间叠层。':'示例中的餐桌和房间检查已通过，可以完成本任务。') : teaching.preparation+'。';
 if(activeTab==='overview') $('#task-content').innerHTML=`<div class="next-step"><small>下一步 · 先完成这一件事</small><strong>${esc(next)}</strong><p>${esc(explanation)}</p><button class="small" data-action="${dining&&!diningResearch?'research':'guide'}">${dining&&!diningResearch?'查看研究准备':'查看操作步骤'} →</button></div><div class="subframe"><h3>◇ 本任务的目标</h3>${dining?checkRow('餐桌可用且可达',diningBuilt,'已有设施直接认可，无需重复建造')+checkRow('原版识别为餐厅',diningBuilt,'以游戏房间叠层的识别结果为准'):`<p>${esc(task.outcome)}</p>`}</div><p style="font-size:12px;line-height:1.8;color:#464852">${esc(teaching.requiredOrOptional)} · ${task.implementation==='existingRuntime'?'任务完成记录与当前设施检查分开保存。':'此项展示教学路线，尚未接入独立自动判定。'}</p>`;
 if(activeTab==='guide') $('#task-content').innerHTML=`<div class="subframe"><h3>开始之前</h3><p>${esc(teaching.preparation)}</p></div>${teaching.steps.map((s,i)=>`<div class="guide-step"><span class="step-number">${i+1}</span><span>${esc(s)}</span></div>`).join('')}<div class="subframe" style="margin-top:16px"><h3>容易卡住的地方</h3><p>${esc(teaching.commonFailures)}</p></div>`;
 if(activeTab==='checks') $('#task-content').innerHTML=`<div class="subframe"><h3>运行核对</h3><p>${esc(teaching.runningChecks)}</p></div><div class="subframe"><h3>完成判定的范围</h3><p>${esc(teaching.completion)}</p></div><div class="subframe"><h3>完成之后</h3><p>${esc(teaching.next)}</p></div>`;
 $('#action-note').textContent=completed.has(taskId)?'已学会 · 仍可查看教学与检查条件':'示例操作，仅改变此演示页面';
 $('#primary').textContent=completed.has(taskId)?'查看完成检查':dining?(!diningResearch?'查看前置研究':!diningBuilt?'演示建成餐厅':'完成任务'):'演示完成任务';
 $('#research').innerHTML=dining?`<div class="target-building"><small>本任务要建造</small><strong>餐桌</strong></div><div class="research-chain">研究台 → 食物制备 → 餐桌<br><span class="tag ${diningResearch?'done':''}">${diningResearch?'研究已完成':'研究未完成'}</span></div><button class="small" data-action="research">查看研究详情 →</button>`:`<div class="target-building"><small>本任务的准备条件</small><strong>${esc(task.title)}</strong></div><div class="research-chain">${esc(teaching.preparation)}</div><button class="small" data-action="preparation">查看准备说明 →</button>`;
 $('#resources').innerHTML=(dining?[['泥土','研究消耗','dirt'],['建筑材料','以建造菜单为准','metal'],['电力','研究台运行','power']]:[['准备资源','按当前任务核对','metal'],['运行供给','查看持续消耗','power']]).map(([name,note,type])=>`<button class="resource-row" data-resource="${type}"><span class="material-dot ${type}"></span><b>${name}</b><span>${note} ›</span></button>`).join('');
}
function checkRow(label,met,note){return `<div class="check-row"><span class="check-icon ${met?'':'pending'}">${met?'✓':'◇'}</span><div>${esc(label)}<small>${esc(note)}</small></div></div>`;}
function researchModal(){modal('前置研究 · 餐桌',`<div class="target-building"><small>为了完成「完善餐厅」，你要建造</small><strong>餐桌</strong></div><h3>研究台 → 食物制备 → 餐桌</h3><p>① 建造研究台，接通电源。<br>② 准备泥土，检查复制人的研究工作权限与通路。<br>③ 在研究界面选择「食物制备」。<br>④ 研究完成后，再建造餐桌。</p><p><strong>研究状态：</strong>${diningResearch?'已完成':'未完成'}（示例）</p><p>已有研究直接认可。游戏中的具体研究点数、材料与解锁关系应读取原版数据。</p>${diningResearch?'':'<button class="pink" id="simulate-research">演示：研究已完成</button>'}`);}
$('#chapters').addEventListener('click',e=>{const b=e.target.closest('[data-chapter]');if(!b)return;chapterIndex=Number(b.dataset.chapter);taskId=catalog.chapters[chapterIndex].mainTaskIds[0];activeTab='overview';render();});
$('#tasks').addEventListener('click',e=>{const b=e.target.closest('[data-task]');if(!b)return;taskId=b.dataset.task;activeTab='overview';render();});
document.querySelectorAll('[data-tab]').forEach(b=>b.addEventListener('click',()=>{activeTab=b.dataset.tab;render();}));
$('#main-filter').onclick=()=>{showAll=false;if(current().track==='optional')taskId=catalog.chapters[chapterIndex].mainTaskIds[0];render();}; $('#all-filter').onclick=()=>{showAll=true;render();};
$('.workspace').addEventListener('click',e=>{const b=e.target.closest('[data-action],[data-resource]');if(!b)return;
 if(b.dataset.action==='research')return researchModal();
 if(b.dataset.action==='guide'){activeTab='guide';render();return;}
 if(b.dataset.action==='preparation')return modal('任务准备 · '+current().title,`<p>${esc(current().teaching.preparation)}</p><p>${esc(current().teaching.runningChecks)}</p>`);
 if(b.dataset.resource){const descriptions={dirt:'泥土用于基础研究。除了库存，还要检查搬运路线、研究台供给和复制人工作权限。',metal:'先查看原版建造菜单的可选材料与用量，再检查可访问库存和施工路线。此演示不编造实际游戏库存。',power:'研究台等设备需要持续供电。检查电力叠层、线路是否连通、发电状态与负载。'};modal('资源介绍',`<h3>${b.querySelector('b').textContent}</h3><p>${taskId==='q01_dining'?descriptions[b.dataset.resource]:esc(current().teaching.preparation)}</p><p><strong>相关任务：</strong>${esc(current().title)}</p>`);}
});
$('#primary').onclick=()=>{if(completed.has(taskId)){activeTab='checks';render();return;}if(taskId==='q01_dining'&&!diningResearch)return researchModal();if(taskId==='q01_dining'&&!diningBuilt){diningBuilt=true;toast('示例：餐桌可用、可达，房间已识别为餐厅。');render();return;}completed.add(taskId);toast('任务完成：「'+current().title+'」 · 可关闭此消息');render();};
$('#modal-content').addEventListener('click',e=>{if(e.target.id==='simulate-research'){diningResearch=true;$('#modal').close();toast('示例：食物制备研究完成，餐桌已解锁。');render();}});
$('#modal-close').onclick=$('#modal-done').onclick=()=>$('#modal').close();
$('#toast button').onclick=()=>{$('#toast').hidden=true;};
$('#help').onclick=()=>modal('界面演示说明','<h3>一个框架，三块工作区</h3><p>左侧选章节与任务；中间看当前目标和分步操作；右侧查看研究、资源和辅助信息。</p><p>你可以切换六章，查看主线与选做任务，打开资源或研究详情，并模拟餐厅任务的完整流程。</p><p>这里的进度、复制人读数和操作均为示例；刷新页面即可重置，不修改游戏或存档。</p>');
$('#crew-details').onclick=()=>modal('复制人监测 · 示例','<h3>米玛</h3><p>生命：100%<br>压力：8%<br>呼吸：100%<br>热量：1,600 kcal</p><p>监测条不显示悬停大弹窗。游戏内可双击头像定位复制人，此浏览器演示只展示信息布局。</p>');
fetch('catalog.json').then(r=>{if(!r.ok)throw new Error('目录读取失败');return r.json();}).then(data=>{catalog=data;render();}).catch(e=>{toast('演示加载失败：'+e.message);});
