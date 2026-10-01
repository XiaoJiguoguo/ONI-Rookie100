'use strict';
let current = null, mode = 'live', busy = false, error = '', epoch = 0, waiting = false;
const $ = id => document.getElementById(id);
function element(tag, text, cls) { const node = document.createElement(tag); if (text !== undefined) node.textContent = text; if (cls) node.className = cls; return node; }
function valid(data) {
  if (!data || data.schemaVersion !== 1 || data.kind !== 'debug.snapshot' || data.modId !== 'ONI-Rookie100' || data.source !== 'game' || typeof data.sessionId !== 'string' || !Number.isInteger(data.sequence) || !Number.isFinite(Date.parse(data.capturedAt))) throw new Error('不是受支持的游戏快照');
  if (!Array.isArray(data.tasks) || !Array.isArray(data.duplicants)) throw new Error('快照缺少任务或复制人列表');
  return data;
}
function accept(data) {
  valid(data);
  if (mode === 'live' && current && current.sessionId === data.sessionId && data.sequence <= current.sequence) return;
  current = data; error = ''; render();
}
function renderCards(id, tasks) {
  const root = $(id); root.replaceChildren();
  for (const task of tasks) {
    const card = element('article', undefined, 'card');
    card.append(element('h3', task.title || task.id));
    card.append(element('div', task.learned ? '学习记录：已掌握' : '学习记录：未掌握', 'history'));
    for (const objective of task.objectives || []) {
      const row = element('div', undefined, 'check');
      row.append(element('span', objective.met === true ? '通过' : objective.met === false ? '未通过' : '未知', 'badge ' + (objective.met === true ? 'yes' : objective.met === false ? 'no' : '')));
      row.append(element('span', objective.detail || objective.label || '任务条件'));
      card.append(row);
    }
    if (!(task.objectives || []).length) card.append(element('p', '知识任务，无设施判定。', 'muted'));
    root.append(card);
  }
}
function render() {
  if (!current || current.state === 'disconnected') { clearReadings(); updateStatus(); if (current) $('raw').textContent = JSON.stringify(current, null, 2); return; }
  const metrics = [['存档', current.colonyKey || '未知'], ['当前星体 ID', current.world >= 0 ? current.world : '未知'], ['采样序号', current.sequence], ['游戏状态', current.paused === true ? '已暂停' : current.paused === false ? '运行中' : '已载入']];
  $('summary').replaceChildren(...metrics.map(([name, value]) => { const n = element('div', undefined, 'metric'); n.append(element('small', name), element('strong', String(value))); return n; }));
  const ids = new Set(['q01', 'q01_bedroom', 'q01_dining']);
  renderCards('living', current.tasks.filter(t => ids.has(t.id)));
  renderCards('others', current.tasks.filter(t => !ids.has(t.id)));
  $('duplicants').replaceChildren();
  const number = (v, suffix) => typeof v === 'number' && Number.isFinite(v) ? Math.round(v).toLocaleString('zh-CN') + suffix : '未知';
  for (const dupe of current.duplicants) { const row = element('tr'); [dupe.name || '未知', number(dupe.health, '%'), number(dupe.stress, '%'), number(dupe.breath, '%'), number(dupe.calories, ' kcal')].forEach(v => row.append(element('td', v))); $('duplicants').append(row); }
  if (!current.duplicants.length) { const row = element('tr'); const cell = element('td', '当前快照没有复制人读数。'); cell.colSpan = 5; row.append(cell); $('duplicants').append(row); }
  $('context').replaceChildren();
  for (const [id, label] of [['context.dlcOwned', '账户拥有的 DLC'], ['context.dlcEnabled', '存档启用的 DLC'], ['context.startWorldId', '出生星体 ID'], ['context.activeWorldId', '当前星体 ID']]) { const fact = (current.facts || {})[id]; const value = fact ? fact.value : null; const node = element('div'); node.append(element('small', label), element('span', value == null ? '未知' : Array.isArray(value) ? value.join('、') || '无' : String(value))); $('context').append(node); }
  $('raw').textContent = JSON.stringify(current, null, 2); updateStatus();
}
function clearReadings() {
  $('summary').replaceChildren(); $('context').replaceChildren(); $('others').replaceChildren();
  $('living').replaceChildren(element('p', '尚无当前游戏任务数据。', 'empty'));
  const row = element('tr'); const cell = element('td', '尚无当前游戏读数。'); cell.colSpan = 5; row.append(cell); $('duplicants').replaceChildren(row);
}
function updateStatus() {
  const panel = document.querySelector('.connection'); panel.className = 'connection';
  if (waiting) { $('connection').textContent = '等待游戏快照'; $('detail').textContent = error; return; }
  if (error) { panel.classList.add('error'); $('connection').textContent = '桥接暂不可用'; $('detail').textContent = error + (current ? '；下方保留上次快照，不代表当前状态。' : ''); return; }
  if (!current) { $('connection').textContent = '等待游戏快照'; return; }
  const age = (Date.now() - Date.parse(current.capturedAt)) / 1000;
  if (current.state === 'disconnected') { $('connection').textContent = '游戏已离开存档'; $('detail').textContent = '已清空任务与复制人读数，等待下次载入。'; return; }
  const stale = age > 10 || age < -5;
  if (stale) panel.classList.add('stale');
  $('connection').textContent = stale ? '快照已过期或电脑时钟不一致' : mode === 'file' ? '已导入游戏快照' : '正在读取本机游戏快照';
  $('detail').textContent = `${mode === 'file' ? '文件导入，不是实时连接。' : '本机只读连接。'}采样时间：${new Date(current.capturedAt).toLocaleString('zh-CN')}；任务条件采样：${current.objectivesCapturedAt ? new Date(current.objectivesCapturedAt).toLocaleTimeString('zh-CN') : '未知'}。`;
}
async function poll() {
  if (mode !== 'live' || busy) return;
  busy = true;
  const started = epoch;
  try { const response = await fetch('/api/snapshot', {cache: 'no-store'}); const data = await response.json(); if (started !== epoch || mode !== 'live') return; waiting = response.status === 404; if (!response.ok) throw new Error(data.error || '无法读取快照'); accept(data); error = ''; }
  catch (e) { if (started === epoch && mode === 'live') error = e.message; } finally { busy = false; updateStatus(); }
}
$('live').addEventListener('click', () => { epoch++; mode = 'live'; current = null; error = ''; waiting = false; render(); poll(); });
$('file').addEventListener('change', async event => { const file = event.target.files[0]; if (!file) return; try { if (file.size > 4 * 1024 * 1024) throw new Error('快照文件过大'); const data = valid(JSON.parse(await file.text())); epoch++; mode = 'file'; current = null; waiting = false; accept(data); } catch(e) { error = e.message; updateStatus(); } });
setInterval(() => { poll(); updateStatus(); }, 1000);
poll();
