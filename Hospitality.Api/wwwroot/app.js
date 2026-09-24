const tokenKey = "hospitality.token";
const appKey = "hospitality.app";
const walletKey = "hospitality.walletId";

const loginView = document.getElementById("login-view");
const chooserView = document.getElementById("chooser-view");
const appView = document.getElementById("app-view");
const feloganView = document.getElementById("felogan-view");
const loginForm = document.getElementById("login-form");
const loginError = document.getElementById("login-error");
const chooserError = document.getElementById("chooser-error");
const txnForm = document.getElementById("txn-form");
const txnError = document.getElementById("txn-error");
const methodSelect = document.getElementById("method");
const openingForm = document.getElementById("opening-form");
const openingError = document.getElementById("opening-error");

let lastWallets = { cashOnHand: 0, gcash: 0, tpc: 0 };
let lastTxns = [];
let methodLabels = {};
let historyRange = "day";
let editingId = null;
let editingOccurredAt = null;
const accountForm = document.getElementById("account-form");
const historyDate = document.getElementById("history-date");
const adminSettings = document.getElementById("admin-settings");
const settingsMenu = document.querySelector("[data-settings-menu]");

let currentUser = null;
let walletSets = [];
let activeWalletId = "";
let editingWalletId = null;
let pendingDelete = null;
const deleteModal = document.getElementById("delete-modal");
const deleteError = document.getElementById("delete-error");
const deleteConfirm = document.getElementById("delete-confirm");

function token() {
  return sessionStorage.getItem(tokenKey);
}

function isAdmin() {
  return (currentUser?.roles || []).includes("Admin");
}

function ledgerQuery(path) {
  const url = new URL(path, window.location.origin);
  if (isAdmin() && activeWalletId) {
    url.searchParams.set("walletId", activeWalletId);
  }
  return `${url.pathname}${url.search}`;
}

function mainWallet() {
  return walletSets.find((wallet) => wallet.isMain) || walletSets[0];
}

function walletOptions(selectedId) {
  return walletSets.map((wallet) =>
    `<option value="${wallet.id}" ${wallet.id === selectedId ? "selected" : ""}>${escapeHtml(wallet.name)}</option>`
  ).join("");
}

async function loadWalletSets() {
  const data = await api("/v1/wallet-sets");
  walletSets = data.items || [];
}

function applyWalletChrome() {
  const wrap = document.querySelector(".wallet-switch");
  const trigger = document.getElementById("wallet-switch");
  const copy = document.getElementById("wallet-switch-copy");
  const menu = document.getElementById("wallet-switch-menu");
  const label = document.getElementById("wallet-label");
  if (isAdmin()) {
    label.hidden = true;
    wrap.hidden = false;
    const stored = sessionStorage.getItem(walletKey);
    const match = walletSets.find((wallet) => wallet.id === stored) || mainWallet();
    activeWalletId = match?.id || "";
    if (activeWalletId) {
      sessionStorage.setItem(walletKey, activeWalletId);
    }
    copy.textContent = match?.name || "Wallet";
    menu.innerHTML = walletSets.map((wallet) =>
      `<button type="button" role="option" data-wallet-id="${wallet.id}" class="${wallet.id === activeWalletId ? "is-active" : ""}">${escapeHtml(wallet.name)}</button>`
    ).join("");
    return;
  }

  wrap.hidden = true;
  closeWalletMenu();
  label.hidden = false;
  label.textContent = currentUser?.assignedWalletName || "";
  activeWalletId = currentUser?.assignedWalletId || "";
}

function closeWalletMenu() {
  const trigger = document.getElementById("wallet-switch");
  const menu = document.getElementById("wallet-switch-menu");
  trigger.setAttribute("aria-expanded", "false");
  menu.hidden = true;
}

function deleteButton(attrs) {
  return `<button type="button" class="text-link danger" ${attrs}>Delete</button>`;
}

function showError(el, message) {
  el.hidden = !message;
  el.textContent = message || "";
}

let toastTimer = null;

function showToast(message, kind = "success") {
  const toast = document.getElementById("app-toast");
  const copy = document.getElementById("app-toast-copy");
  clearTimeout(toastTimer);
  copy.textContent = message || "";
  toast.hidden = !message;
  toast.classList.toggle("toast-error", Boolean(message) && kind === "error");
  if (message) {
    toastTimer = setTimeout(() => showToast(""), 2800);
  }
}

document.getElementById("app-toast").addEventListener("click", () => showToast(""));

function peso(value) {
  return new Intl.NumberFormat("en-PH", { style: "currency", currency: "PHP" }).format(value);
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function serviceLabel(method) {
  return methodLabels[method] || String(method || "").replaceAll("_", " ");
}

function formatHistoryDate(iso) {
  const [year, month, day] = String(iso).split("-").map(Number);
  if (!year || !month || !day) {
    return iso;
  }
  return new Date(Date.UTC(year, month - 1, day)).toLocaleDateString("en-PH", {
    month: "short",
    day: "numeric",
    year: "numeric",
    timeZone: "UTC"
  });
}

function showHistoryTotals(html, isError = false) {
  const totals = document.getElementById("totals");
  totals.className = isError ? "history-summary-error" : "history-summary";
  totals.innerHTML = html;
}

function showHistoryError(message) {
  showHistoryTotals(escapeHtml(message), true);
}

function manilaDate() {
  return new Date().toLocaleDateString("en-CA", { timeZone: "Asia/Manila" });
}

function manilaTime() {
  return new Date().toLocaleTimeString("en-GB", {
    timeZone: "Asia/Manila",
    hour12: false,
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit"
  });
}

function selectedDate() {
  return historyDate.value || manilaDate();
}

function suggestedProfit(method, amount) {
  if (!Number.isFinite(amount) || amount <= 0) {
    return null;
  }
  if (["top_up_gcash", "top_up_tpc", "add_cash_capital", "expenses", "bank_cash_deposit"].includes(method)) {
    return 0;
  }
  if (method === "load_via_gcash" || method === "load_via_tpc") {
    return amount < 100 ? 5 : 10;
  }
  if (method === "cash_in" || method === "cash_out" || method === "cash_out_with_charge") {
    if (amount < 100) {
      return 5;
    }
    const rounded = Math.round((amount * 0.02) / 10) * 10;
    return Math.max(10, rounded);
  }
  return 0;
}

function updateProfitHint() {
  const profit = document.getElementById("profit");
  const amount = Number(document.getElementById("amount").value);
  const suggested = suggestedProfit(methodSelect.value, amount);
  profit.placeholder = suggested === null ? "" : String(suggested);
  const cashbackField = document.getElementById("cashback-field");
  const isTpcLoad = methodSelect.value === "load_via_tpc";
  cashbackField.hidden = !isTpcLoad;
  if (!isTpcLoad) {
    document.getElementById("cashback").value = "";
  }
}

async function api(path, options = {}) {
  const headers = { ...(options.headers || {}) };
  const accessToken = token();
  if (accessToken) {
    headers.Authorization = `Bearer ${accessToken}`;
  }
  if (options.body && !headers["Content-Type"]) {
    headers["Content-Type"] = "application/json";
  }
  const response = await fetch(path, { ...options, headers });
  const text = await response.text();
  const data = text ? JSON.parse(text) : null;
  if (!response.ok) {
    throw new Error(data?.error?.message || `Request failed (${response.status})`);
  }
  return data;
}

async function signIn(username, password) {
  const data = await api("/v1/auth/login", {
    method: "POST",
    body: JSON.stringify({ username, password })
  });
  sessionStorage.setItem(tokenKey, data.access_token);
}

function signOut() {
  sessionStorage.removeItem(tokenKey);
  sessionStorage.removeItem(appKey);
  sessionStorage.removeItem(walletKey);
  currentUser = null;
  loginView.hidden = false;
  chooserView.hidden = true;
  appView.hidden = true;
  feloganView.hidden = true;
}

function showChooser() {
  loginView.hidden = true;
  appView.hidden = true;
  feloganView.hidden = true;
  chooserView.hidden = false;
}

function switchApp() {
  sessionStorage.removeItem(appKey);
  showChooser();
}

function showSettingsScreen(name) {
  settingsMenu.hidden = Boolean(name);
  document.querySelectorAll("[data-settings-screen]").forEach((screen) => {
    screen.hidden = screen.dataset.settingsScreen !== name;
  });
  if (name === "users") {
    loadUsers().catch((error) => showError(document.getElementById("users-error"), error.message));
  }
  if (name === "opening") {
    fillOpeningWallets();
  }
  if (name === "wallets") {
    renderWalletSets().catch((error) => showError(document.getElementById("wallet-sets-error"), error.message));
  }
  if (name === "account") {
    fillAccountWallets();
    syncAccountWalletField();
  }
}

function fillOpeningWallets() {
  document.getElementById("open-cash").value = lastWallets.cashOnHand;
  document.getElementById("open-gcash").value = lastWallets.gcash;
  document.getElementById("open-tpc").value = lastWallets.tpc;
  showError(openingError, "");
}

function showPanel(name) {
  document.querySelectorAll("[data-panel]").forEach((panel) => {
    panel.hidden = panel.dataset.panel !== name;
  });
  document.querySelectorAll("[data-nav]").forEach((button) => {
    button.classList.toggle("active", button.dataset.nav === name);
  });
  if (name === "history") {
    refreshLedger().catch((error) => {
      showHistoryError(error.message);
    });
  }
  if (name === "settings") {
    showSettingsScreen("");
  }
}

async function loadApp() {
  const label = `${currentUser.username} · ${(currentUser.roles || []).join(", ")}`;
  document.getElementById("who").textContent = label;
  document.getElementById("accounts-current").textContent = `Current user: ${currentUser.username}`;
  document.getElementById("profile-username").textContent = currentUser.username;
  document.getElementById("profile-role").textContent = (currentUser.roles || []).join(", ");
  adminSettings.hidden = !isAdmin();
  document.querySelector('[data-nav="settings"]').hidden = !isAdmin();
  document.getElementById("switch-app").hidden = !isAdmin();
  historyDate.value = manilaDate();
  const txnDate = document.getElementById("txn-date");
  txnDate.value = manilaDate();
  txnDate.max = manilaDate();

  await loadWalletSets();
  applyWalletChrome();

  const methods = await api("/v1/transaction-methods");
  methodLabels = Object.fromEntries((methods.items || []).map((item) => [item.method, item.label]));
  methodSelect.innerHTML = methods.items
    .map((item) => `<option value="${item.method}">${item.label}</option>`)
    .join("");
  updateProfitHint();

  await refreshLedger();
  showPanel("txn");
  loginView.hidden = true;
  chooserView.hidden = true;
  feloganView.hidden = true;
  appView.hidden = false;
}

async function routeAuthenticatedUser(fromLogin = false) {
  currentUser = await api("/v1/users/current");
  if (!isAdmin()) {
    sessionStorage.setItem(appKey, "counter");
    await loadApp();
    return;
  }

  if (fromLogin) {
    sessionStorage.removeItem(appKey);
    showChooser();
    return;
  }

  const selectedApp = sessionStorage.getItem(appKey);
  if (selectedApp === "felogan") {
    await window.openFeloganApp();
  } else if (selectedApp === "counter") {
    await loadApp();
  } else {
    showChooser();
  }
}

async function refreshLedger() {
  const date = selectedDate();
  const [wallets, list] = await Promise.all([
    api(ledgerQuery("/v1/wallets")),
    api(ledgerQuery(`/v1/transactions?date=${date}&range=${historyRange}`))
  ]);

  lastWallets = wallets;
  document.getElementById("wallets").innerHTML = `
    <div><span>Cash</span><strong>${peso(wallets.cashOnHand)}</strong></div>
    <div><span>GCash</span><strong>${peso(wallets.gcash)}</strong></div>
    <div><span>TPC</span><strong>${peso(wallets.tpc)}</strong></div>`;
  if (!document.querySelector('[data-settings-screen="opening"]').hidden) {
    fillOpeningWallets();
  }

  lastTxns = list.items || [];
  const periodLabel = list.from === list.to ? "Date" : "Period";
  const period = list.from === list.to
    ? formatHistoryDate(list.from)
    : `${formatHistoryDate(list.from)} – ${formatHistoryDate(list.to)}`;
  showHistoryTotals(`
    <div>
      <span>${periodLabel}</span>
      <strong>${escapeHtml(period)}</strong>
    </div>
    <div>
      <span>Transactions</span>
      <strong>${list.totals.count}</strong>
    </div>
    <div>
      <span>Profit</span>
      <strong>${peso(list.totals.profit)}</strong>
    </div>`);

  document.getElementById("txns").innerHTML = lastTxns.length
    ? `<table class="history-table">
        <thead>
          <tr>
            <th>Date</th>
            <th>Time</th>
            <th>Service</th>
            <th>Amount</th>
            <th>Profit</th>
            <th>In charge</th>
            <th>Note</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          ${lastTxns.map((row) => {
            const when = new Date(row.occurredAt);
            const date = when.toLocaleDateString("en-PH", {
              timeZone: "Asia/Manila",
              month: "short",
              day: "numeric",
              year: "numeric"
            });
            const time = when.toLocaleTimeString("en-PH", {
              timeZone: "Asia/Manila",
              hour: "2-digit",
              minute: "2-digit"
            });
            const profit = `${peso(row.profit)}${row.profitInGcash ? " · GCash" : ""}`;
            const amount = row.cashback > 0
              ? `${peso(row.amount)} · CB ${peso(row.cashback)}`
              : peso(row.amount);
            const deleteBtn = isAdmin()
              ? deleteButton(`data-delete="${row.id}"`)
              : "";
            return `<tr>
              <td data-label="Date">${date}</td>
              <td data-label="Time">${time}</td>
              <td data-label="Service">${escapeHtml(serviceLabel(row.method))}</td>
              <td data-label="Amount" class="num">${amount}</td>
              <td data-label="Profit" class="num">${profit}</td>
              <td data-label="In charge">${escapeHtml(row.inCharge.username)}</td>
              <td data-label="Note">${row.note
                ? `<button type="button" class="text-link" data-note="${row.id}">View</button>`
                : "—"}</td>
              <td data-label="Actions" class="actions">
                <div class="item-actions">
                  <button type="button" class="text-link" data-edit="${row.id}">Edit</button>
                  ${deleteBtn}
                </div>
              </td>
            </tr>`;
          }).join("")}
        </tbody>
      </table>`
    : `<p class="muted history-empty">No transactions for ${escapeHtml(period)}.</p>`;
}

function occurredAtForSave(txnDate) {
  if (editingOccurredAt) {
    const time = new Date(editingOccurredAt).toLocaleTimeString("en-GB", {
      timeZone: "Asia/Manila",
      hour12: false,
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit"
    });
    return `${txnDate}T${time}+08:00`;
  }

  return `${txnDate}T${manilaTime()}+08:00`;
}

function setEditing(id, occurredAt = null) {
  editingId = id;
  editingOccurredAt = occurredAt;
  document.getElementById("txn-heading").textContent = id ? "Edit transaction" : "New transaction";
  document.getElementById("txn-save").textContent = id ? "Save changes" : "Save";
  document.getElementById("txn-cancel").hidden = !id;
}

function startEdit(row) {
  setEditing(row.id, row.occurredAt);
  document.getElementById("txn-date").value = new Date(row.occurredAt).toLocaleDateString("en-CA", {
    timeZone: "Asia/Manila"
  });
  methodSelect.value = row.method;
  document.getElementById("amount").value = row.amount;
  document.getElementById("profit").value = row.profit;
  document.getElementById("profit-gcash").checked = row.profitInGcash;
  document.getElementById("cashback").value = row.cashback || "";
  document.getElementById("note").value = row.note || "";
  updateProfitHint();
  showPanel("txn");
}

async function loadUsers() {
  showError(document.getElementById("users-error"), "");
  const data = await api("/v1/staff");
  const users = document.getElementById("users");
  const items = data.items || [];
  const added = items.filter((user) => user.id !== currentUser?.id);
  if (added.length === 0) {
    users.innerHTML = `<p class="muted history-empty">No accounts yet.</p>`;
    return;
  }

  users.innerHTML = `<table class="history-table account-table">
    <thead>
      <tr>
        <th>Username</th>
        <th>Wallet</th>
        <th>Role</th>
        <th>Actions</th>
      </tr>
    </thead>
    <tbody>
      ${added.map((user) => {
        const role = (user.roles || [])[0] || "Staff";
        const walletCell = role === "Staff"
          ? `<select data-user-wallet="${user.id}">${walletOptions(user.assignedWalletId)}</select>`
          : `<span class="muted">—</span>`;
        return `<tr>
          <td data-label="Username">${escapeHtml(user.username)}</td>
          <td data-label="Wallet">${walletCell}</td>
          <td data-label="Role">
            <select data-user-id="${user.id}">
              <option value="Staff" ${role === "Staff" ? "selected" : ""}>Staff</option>
              <option value="Admin" ${role === "Admin" ? "selected" : ""}>Admin</option>
            </select>
          </td>
          <td data-label="Actions">${deleteButton(`data-delete-user="${user.id}" data-username="${escapeHtml(user.username)}"`)}</td>
        </tr>`;
      }).join("")}
    </tbody>
  </table>`;
}

loginForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  showError(loginError, "");
  const submit = loginForm.querySelector("button[type=submit]");
  submit.disabled = true;
  submit.textContent = "Signing in…";
  try {
    await signIn(document.getElementById("username").value.trim(), document.getElementById("password").value);
    await routeAuthenticatedUser(true);
  } catch (error) {
    showError(loginError, error.message);
  } finally {
    submit.disabled = false;
    submit.textContent = "Sign in";
  }
});

document.getElementById("logout").addEventListener("click", signOut);
document.getElementById("chooser-logout").addEventListener("click", signOut);
document.getElementById("felogan-logout").addEventListener("click", signOut);
document.getElementById("switch-app").addEventListener("click", switchApp);
document.getElementById("felogan-switch-app").addEventListener("click", switchApp);
document.getElementById("choose-counter").addEventListener("click", async () => {
  showError(chooserError, "");
  try {
    await loadApp();
    sessionStorage.setItem(appKey, "counter");
  } catch (error) {
    showError(chooserError, error.message);
  }
});
document.getElementById("choose-felogan").addEventListener("click", async () => {
  showError(chooserError, "");
  try {
    await window.openFeloganApp();
    sessionStorage.setItem(appKey, "felogan");
  } catch (error) {
    showError(chooserError, error.message);
  }
});

document.querySelectorAll("[data-nav]").forEach((button) => {
  button.addEventListener("click", () => {
    if (button.dataset.nav === "txn") {
      txnForm.reset();
      setEditing(null);
      document.getElementById("txn-date").value = manilaDate();
      document.getElementById("txn-date").max = manilaDate();
      updateProfitHint();
    }
    showPanel(button.dataset.nav);
  });
});

document.querySelectorAll("[data-settings-page]").forEach((button) => {
  button.addEventListener("click", () => showSettingsScreen(button.dataset.settingsPage));
});

document.querySelectorAll("[data-settings-back]").forEach((button) => {
  button.addEventListener("click", () => showSettingsScreen(""));
});

historyDate.addEventListener("change", () => {
  refreshLedger().catch((error) => {
    showHistoryError(error.message);
  });
});

methodSelect.addEventListener("change", updateProfitHint);
document.getElementById("amount").addEventListener("input", updateProfitHint);

document.querySelectorAll("[data-range]").forEach((button) => {
  button.addEventListener("click", () => {
    historyRange = button.dataset.range;
    document.querySelectorAll("[data-range]").forEach((tab) => {
      tab.classList.toggle("active", tab === button);
    });
    refreshLedger().catch((error) => {
      showHistoryError(error.message);
    });
  });
});

txnForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  showError(txnError, "");
  const profitRaw = document.getElementById("profit").value;
  const txnDate = document.getElementById("txn-date").value || manilaDate();
  const body = {
    method: methodSelect.value,
    amount: Number(document.getElementById("amount").value),
    profitInGcash: document.getElementById("profit-gcash").checked,
    note: document.getElementById("note").value.trim(),
    occurredAt: occurredAtForSave(txnDate),
    clientRequestId: crypto.randomUUID()
  };
  if (profitRaw !== "") {
    body.profit = Number(profitRaw);
  }
  if (methodSelect.value === "load_via_tpc") {
    const cashbackRaw = document.getElementById("cashback").value;
    body.cashback = cashbackRaw === "" ? 0 : Number(cashbackRaw);
  }
  try {
    const wasEdit = Boolean(editingId);
    if (editingId) {
      await api(ledgerQuery(`/v1/transactions/${editingId}`), { method: "PUT", body: JSON.stringify(body) });
    } else {
      await api(ledgerQuery("/v1/transactions"), { method: "POST", body: JSON.stringify(body) });
    }
    txnForm.reset();
    setEditing(null);
    document.getElementById("txn-date").value = txnDate;
    document.getElementById("txn-date").max = manilaDate();
    updateProfitHint();
    await refreshLedger();
    if (wasEdit) {
      showPanel("history");
    }
    showToast(wasEdit ? "Transaction updated." : "Transaction saved.");
  } catch (error) {
    showError(txnError, error.message);
    showToast(error.message, "error");
  }
});

document.getElementById("txn-cancel").addEventListener("click", () => {
  txnForm.reset();
  setEditing(null);
  document.getElementById("txn-date").value = manilaDate();
  updateProfitHint();
  showPanel("history");
});

function openDeleteModal({ kind, id, title, copy }) {
  pendingDelete = { kind, id };
  document.getElementById("delete-title").textContent = title;
  document.getElementById("delete-copy").textContent = copy;
  showError(deleteError, "");
  deleteConfirm.disabled = false;
  deleteConfirm.textContent = "Delete";
  deleteModal.hidden = false;
  document.getElementById("delete-cancel").focus();
}

function closeDeleteModal() {
  pendingDelete = null;
  document.getElementById("delete-title").textContent = "Delete transaction";
  deleteConfirm.disabled = false;
  deleteConfirm.textContent = "Delete";
  showError(deleteError, "");
  deleteModal.hidden = true;
}

const noteModal = document.getElementById("note-modal");

function openNoteModal(note) {
  document.getElementById("note-copy").textContent = note;
  noteModal.hidden = false;
  document.getElementById("note-close").focus();
}

function closeNoteModal() {
  noteModal.hidden = true;
}

document.getElementById("txns").addEventListener("click", (event) => {
  const note = event.target.closest("[data-note]");
  if (note) {
    const row = lastTxns.find((item) => String(item.id) === note.dataset.note);
    if (row?.note) {
      openNoteModal(row.note);
    }
    return;
  }

  const edit = event.target.closest("[data-edit]");
  if (edit) {
    const row = lastTxns.find((item) => String(item.id) === edit.dataset.edit);
    if (row) {
      startEdit(row);
    }
    return;
  }

  const remove = event.target.closest("[data-delete]");
  if (!remove) {
    return;
  }

  const row = lastTxns.find((item) => String(item.id) === remove.dataset.delete);
  if (row) {
    openDeleteModal({
      kind: "txn",
      id: row.id,
      title: "Delete transaction",
      copy: `Delete ${serviceLabel(row.method)} · ${peso(row.amount)}? This cannot be undone, and wallets will update.`
    });
  }
});

document.getElementById("delete-cancel").addEventListener("click", closeDeleteModal);
deleteModal.addEventListener("click", (event) => {
  if (event.target.closest("[data-delete-cancel]")) {
    closeDeleteModal();
  }
});

noteModal.addEventListener("click", (event) => {
  if (event.target.closest("[data-note-close]")) {
    closeNoteModal();
  }
});

document.addEventListener("keydown", (event) => {
  if (event.key !== "Escape") {
    return;
  }
  if (!noteModal.hidden) {
    closeNoteModal();
    return;
  }
  if (!deleteModal.hidden) {
    closeDeleteModal();
  }
});

deleteConfirm.addEventListener("click", async () => {
  if (!pendingDelete) {
    return;
  }

  showError(deleteError, "");
  deleteConfirm.disabled = true;
  deleteConfirm.textContent = "Deleting…";
  try {
    if (pendingDelete.kind === "user") {
      await api(`/v1/users/${pendingDelete.id}`, { method: "DELETE" });
      closeDeleteModal();
      await loadUsers();
      return;
    }

    if (pendingDelete.kind === "purchase") {
      const id = pendingDelete.id;
      await window.deleteFeloganPurchase(id);
      closeDeleteModal();
      return;
    }

    if (pendingDelete.kind === "serving") {
      const id = pendingDelete.id;
      await window.deleteFeloganServing(id);
      closeDeleteModal();
      return;
    }

    await api(ledgerQuery(`/v1/transactions/${pendingDelete.id}`), { method: "DELETE" });
    closeDeleteModal();
    await refreshLedger();
  } catch (error) {
    deleteConfirm.disabled = false;
    deleteConfirm.textContent = "Delete";
    showError(deleteError, error.message);
  }
});

openingForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  showError(openingError, "");
  try {
    await api(ledgerQuery("/v1/wallets/opening"), {
      method: "PUT",
      body: JSON.stringify({
        cashOnHand: Number(document.getElementById("open-cash").value),
        gcash: Number(document.getElementById("open-gcash").value),
        tpc: Number(document.getElementById("open-tpc").value)
      })
    });
    await refreshLedger();
    showToast("Wallets saved.");
  } catch (error) {
    showError(openingError, error.message);
    showToast(error.message, "error");
  }
});

accountForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const errorEl = document.getElementById("account-error");
  showError(errorEl, "");
  try {
    await api("/v1/staff", {
      method: "POST",
      body: JSON.stringify({
        username: document.getElementById("account-username").value.trim(),
        password: document.getElementById("account-password").value,
        role: document.getElementById("account-role").value,
        ...(document.getElementById("account-role").value === "Staff"
          ? { walletId: document.getElementById("account-wallet").value }
          : {})
      })
    });
    accountForm.reset();
    showSettingsScreen("users");
    showToast("Account created.");
  } catch (error) {
    showError(errorEl, error.message);
    showToast(error.message, "error");
  }
});

document.getElementById("users").addEventListener("click", (event) => {
  const remove = event.target.closest("[data-delete-user]");
  if (!remove) {
    return;
  }

  openDeleteModal({
    kind: "user",
    id: remove.dataset.deleteUser,
    title: "Delete account",
    copy: `Delete ${remove.dataset.username}? They will no longer be able to sign in.`
  });
});

document.getElementById("users").addEventListener("change", async (event) => {
  const walletSelect = event.target.closest("select[data-user-wallet]");
  const roleSelect = event.target.closest("select[data-user-id]");
  const errorEl = document.getElementById("users-error");
  showError(errorEl, "");
  try {
    if (walletSelect) {
      await api(`/v1/users/${walletSelect.dataset.userWallet}/wallet`, {
        method: "PATCH",
        body: JSON.stringify({ walletId: walletSelect.value })
      });
      showToast("Wallet assigned.");
      await loadUsers();
      return;
    }
    if (!roleSelect) {
      return;
    }
    const body = { role: roleSelect.value };
    if (roleSelect.value === "Staff") {
      const assigned = roleSelect.closest("tr")?.querySelector("select[data-user-wallet]");
      body.walletId = assigned?.value || mainWallet()?.id;
    }
    await api(`/v1/users/${roleSelect.dataset.userId}/role`, {
      method: "PATCH",
      body: JSON.stringify(body)
    });
    await loadUsers();
  } catch (error) {
    showError(errorEl, error.message);
    showToast(error.message, "error");
    loadUsers().catch(() => {});
  }
});

document.querySelectorAll("[data-clear]").forEach((button) => {
  button.addEventListener("click", () => {
    const field = button.closest(".field-with-actions")?.querySelector("input");
    if (!field) {
      return;
    }
    field.value = "";
    field.focus();
  });
});

document.querySelectorAll("[data-password-toggle]").forEach((button) => {
  button.addEventListener("click", () => {
    const field = button.closest(".field-with-actions")?.querySelector("input");
    if (!field) {
      return;
    }
    const show = field.type === "password";
    field.type = show ? "text" : "password";
    button.setAttribute("aria-label", show ? "Hide password" : "Show password");
    button.querySelector(".eye-open").hidden = show;
    button.querySelector(".eye-shut").hidden = !show;
  });
});

function fillAccountWallets() {
  const select = document.getElementById("account-wallet");
  select.innerHTML = walletOptions(select.value || mainWallet()?.id || "");
}

function syncAccountWalletField() {
  const staff = document.getElementById("account-role").value === "Staff";
  document.getElementById("account-wallet-field").hidden = !staff;
  document.getElementById("account-wallet").required = staff;
}

function resetWalletSetForm() {
  editingWalletId = null;
  document.getElementById("wallet-set-form").reset();
  document.getElementById("wallet-set-save").textContent = "Add wallet";
  document.getElementById("wallet-set-cancel").hidden = true;
}

async function renderWalletSets() {
  showError(document.getElementById("wallet-sets-error"), "");
  await loadWalletSets();
  applyWalletChrome();
  fillAccountWallets();
  const list = document.getElementById("wallet-sets-list");
  if (!walletSets.length) {
    list.innerHTML = `<p class="muted">No wallets yet.</p>`;
    return;
  }
  list.innerHTML = walletSets.map((wallet) => `<div class="item">
    <b>${escapeHtml(wallet.name)}</b>
    <div class="item-actions">
      <button type="button" class="text-link" data-wallet-edit="${wallet.id}">Edit</button>
      ${wallet.isMain ? "" : `<button type="button" class="text-link danger" data-wallet-delete="${wallet.id}">Delete</button>`}
    </div>
  </div>`).join("");
}

document.getElementById("wallet-switch").addEventListener("click", (event) => {
  event.stopPropagation();
  const trigger = event.currentTarget;
  const menu = document.getElementById("wallet-switch-menu");
  const open = menu.hidden;
  trigger.setAttribute("aria-expanded", open ? "true" : "false");
  menu.hidden = !open;
});

document.getElementById("wallet-switch-menu").addEventListener("click", async (event) => {
  const option = event.target.closest("[data-wallet-id]");
  if (!option) {
    return;
  }
  activeWalletId = option.dataset.walletId;
  sessionStorage.setItem(walletKey, activeWalletId);
  closeWalletMenu();
  applyWalletChrome();
  try {
    await refreshLedger();
  } catch (error) {
    showToast(error.message, "error");
  }
});

document.addEventListener("click", (event) => {
  if (!event.target.closest(".wallet-switch")) {
    closeWalletMenu();
  }
});

document.getElementById("account-role").addEventListener("change", syncAccountWalletField);

document.getElementById("wallet-set-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const errorEl = document.getElementById("wallet-sets-error");
  showError(errorEl, "");
  const name = document.getElementById("wallet-set-name").value.trim();
  try {
    if (editingWalletId) {
      await api(`/v1/wallet-sets/${editingWalletId}`, {
        method: "PUT",
        body: JSON.stringify({ name })
      });
      showToast("Wallet updated.");
    } else {
      await api("/v1/wallet-sets", {
        method: "POST",
        body: JSON.stringify({ name })
      });
      showToast("Wallet saved.");
    }
    resetWalletSetForm();
    await renderWalletSets();
  } catch (error) {
    showError(errorEl, error.message);
    showToast(error.message, "error");
  }
});

document.getElementById("wallet-set-cancel").addEventListener("click", () => {
  resetWalletSetForm();
  showError(document.getElementById("wallet-sets-error"), "");
});

document.getElementById("wallet-sets-list").addEventListener("click", async (event) => {
  const edit = event.target.closest("[data-wallet-edit]");
  const remove = event.target.closest("[data-wallet-delete]");
  if (edit) {
    const wallet = walletSets.find((item) => item.id === edit.dataset.walletEdit);
    if (!wallet) {
      return;
    }
    editingWalletId = wallet.id;
    document.getElementById("wallet-set-name").value = wallet.name;
    document.getElementById("wallet-set-save").textContent = "Save changes";
    document.getElementById("wallet-set-cancel").hidden = false;
    return;
  }
  if (!remove) {
    return;
  }
  const errorEl = document.getElementById("wallet-sets-error");
  showError(errorEl, "");
  try {
    await api(`/v1/wallet-sets/${remove.dataset.walletDelete}`, { method: "DELETE" });
    if (activeWalletId === remove.dataset.walletDelete) {
      sessionStorage.removeItem(walletKey);
    }
    showToast("Wallet deleted.");
    resetWalletSetForm();
    await renderWalletSets();
    await refreshLedger();
  } catch (error) {
    showError(errorEl, error.message);
    showToast(error.message, "error");
  }
});

window.hospitalityApi = api;
window.hospitalityToast = showToast;
window.hospitalityCurrentUser = () => currentUser;
window.hospitalityPeso = peso;
window.hospitalityEscapeHtml = escapeHtml;
window.openHospitalityNote = openNoteModal;
window.openHospitalityDelete = openDeleteModal;
window.bootHospitality = () => {
  const hasToken = Boolean(token());
  if (hasToken) {
    routeAuthenticatedUser().catch(signOut);
  }
};
