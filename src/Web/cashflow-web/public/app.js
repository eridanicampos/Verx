'use strict';

const state = { accessToken: null, user: null };
const $ = (id) => document.getElementById(id);
const out = (data) => { $('output').textContent = typeof data === 'string' ? data : JSON.stringify(data, null, 2); };
const brl = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

async function auth(path, body) {
  const res = await fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'same-origin',
    body: JSON.stringify({ ...body, useCookie: true }),
  });
  if (!res.ok) return false;
  const data = await res.json();
  state.accessToken = data.accessToken;
  const claims = JSON.parse(atob(data.accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
  state.user = claims.name;
  render();
  return true;
}

const refresh = () => auth('/auth/refresh', {});

async function api(path, options = {}, retried = false) {
  const headers = { ...(options.headers || {}), Authorization: `Bearer ${state.accessToken}` };
  if (options.body) headers['Content-Type'] = 'application/json';
  const res = await fetch(path, { ...options, headers, credentials: 'same-origin' });
  if (res.status === 401 && !retried && (await refresh())) return api(path, options, true);
  const text = await res.text();
  const body = text ? JSON.parse(text) : null;
  if (!res.ok) throw Object.assign(new Error(body?.detail || res.statusText), { status: res.status, body });
  return { body, headers: res.headers, status: res.status };
}

function render() {
  const logged = !!state.accessToken;
  $('login-view').hidden = logged;
  $('app-view').hidden = !logged;
  $('logout').hidden = !logged;
  $('session').textContent = logged ? `conectado como ${state.user}` : 'não autenticado';
}

const handle = (fn) => async (e) => {
  e.preventDefault();
  try { await fn(Object.fromEntries(new FormData(e.target))); }
  catch (err) { out(`Erro ${err.status ?? ''}: ${err.message}\n${err.body ? JSON.stringify(err.body, null, 2) : ''}`); }
};

$('login-form').addEventListener('submit', handle(async (f) => {
  if (!(await auth('/auth/token', { username: f.username, password: f.password }))) out('Credenciais inválidas ou conta bloqueada.');
  else out('Autenticado.');
}));

$('logout').addEventListener('click', async () => {
  await fetch('/auth/revoke', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}', credentials: 'same-origin' });
  state.accessToken = null; state.user = null; render(); out('Sessão encerrada.');
});

$('account-form').addEventListener('submit', handle(async (f) => {
  const { body } = await api('/api/v1/accounts', { method: 'POST', body: JSON.stringify({ ...f, overdraftLimit: Number(f.overdraftLimit || 0) }) });
  document.querySelectorAll('input[name=accountId]').forEach((i) => { i.value = body.id; });
  out(body);
}));

let pendingKey = null;
$('tx-form').addEventListener('submit', handle(async (f) => {
  pendingKey ??= crypto.randomUUID();
  const { body, headers } = await api(`/api/v1/accounts/${f.accountId}/transactions`, {
    method: 'POST',
    headers: { 'Idempotency-Key': pendingKey },
    body: JSON.stringify({ type: f.type, amount: Number(f.amount), description: f.description || null }),
  });
  pendingKey = null;
  out({ ...body, replayed: headers.get('Idempotency-Replayed') === 'true' });
}));
$('tx-form').addEventListener('input', () => { pendingKey = null; });

$('balance-form').addEventListener('submit', handle(async (f) => {
  const at = f.at ? `?at=${encodeURIComponent(new Date(f.at).toISOString())}` : '';
  const { body } = await api(`/api/v1/accounts/${f.accountId}/balance${at}`);
  out(`Saldo: ${brl.format(body.balance)}\nCréditos: ${brl.format(body.totalCredits)} · Débitos: ${brl.format(body.totalDebits)}\nEm: ${body.at}\nÚltimo lançamento consolidado: ${body.lastEntryAt ?? '—'} (consistência ${body.consistency})`);
}));

$('daily-form').addEventListener('submit', handle(async (f) => {
  const accountId = $('balance-form').accountId.value;
  const { body } = await api(`/api/v1/accounts/${accountId}/daily-balances?from=${f.from}&to=${f.to}`);
  const tbody = $('daily-table').querySelector('tbody');
  tbody.replaceChildren(...body.map((d) => {
    const tr = document.createElement('tr');
    [d.date, brl.format(d.totalCredits), brl.format(d.totalDebits), brl.format(d.net), brl.format(d.closingBalance)]
      .forEach((v) => { const td = document.createElement('td'); td.textContent = v; tr.appendChild(td); });
    return tr;
  }));
  $('daily-table').hidden = false;
  out(`${body.length} dia(s) com movimento.`);
}));

refresh().then((ok) => { if (!ok) render(); });
