const tokenKey = "hospitality.token";

const loginView = document.getElementById("login-view");
const appView = document.getElementById("app-view");
const loginForm = document.getElementById("login-form");
const loginError = document.getElementById("login-error");
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

function deleteButton(attrs) {
  return `<button type="button" class="text-link danger" ${attrs}>Delete</button>`;
}

function showError(el, message) {
  el.hidden = !message;
  el.textContent = message || "";
}

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
}

async function api(path, options = {}) {
  const headers = { ...(options.headers || {}) };
  if (token()) {
    headers.Authorization = `Bearer ${token()}`;
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
  currentUser = null;
  loginView.hidden = false;
  appView.hidden = true;
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
  currentUser = await api("/v1/users/current");
  const label = `${currentUser.username} · ${(currentUser.roles || []).join(", ")}`;
  document.getElementById("who").textContent = label;
  document.getElementById("accounts-current").textContent = `Current user: ${currentUser.username}`;
  document.getElementById("profile-username").textContent = currentUser.username;
  document.getElementById("profile-role").textContent = (currentUser.roles || []).join(", ");
  adminSettings.hidden = !isAdmin();
  document.querySelector('[data-nav="settings"]').hidden = !isAdmin();
  historyDate.value = manilaDate();
  const txnDate = document.getElementById("txn-date");
  txnDate.value = manilaDate();
  txnDate.max = manilaDate();

  const methods = await api("/v1/transaction-methods");
  methodLabels = Object.fromEntries((methods.items || []).map((item) => [item.method, item.label]));
  methodSelect.innerHTML = methods.items
    .map((item) => `<option value="${item.method}">${item.label}</option>`)
    .join("");
  updateProfitHint();

  await refreshLedger();
  showPanel("txn");
  loginView.hidden = true;
  appView.hidden = false;
}

async function refreshLedger() {
  const date = selectedDate();
  const [wallets, list] = await Promise.all([
    api("/v1/wallets"),
    api(`/v1/transactions?date=${date}&range=${historyRange}`)
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
            const deleteBtn = isAdmin()
              ? deleteButton(`data-delete="${row.id}"`)
              : "";
            return `<tr>
              <td data-label="Date">${date}</td>
              <td data-label="Time">${time}</td>
              <td data-label="Service">${escapeHtml(serviceLabel(row.method))}</td>
              <td data-label="Amount" class="num">${peso(row.amount)}</td>
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
    users.innerHTML = `<li class="muted empty">No accounts yet.</li>`;
    return;
  }

  users.innerHTML = added.map((user) => {
    const role = (user.roles || [])[0] || "Staff";
    return `<li>
      <span>${escapeHtml(user.username)}</span>
      <span class="account-actions">
        <select data-user-id="${user.id}">
          <option value="Staff" ${role === "Staff" ? "selected" : ""}>Staff</option>
          <option value="Admin" ${role === "Admin" ? "selected" : ""}>Admin</option>
        </select>
        ${deleteButton(`data-delete-user="${user.id}" data-username="${escapeHtml(user.username)}"`)}
      </span>
    </li>`;
  }).join("");
}

loginForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  showError(loginError, "");
  const submit = loginForm.querySelector("button[type=submit]");
  submit.disabled = true;
  submit.textContent = "Signing in…";
  try {
    await signIn(document.getElementById("username").value.trim(), document.getElementById("password").value);
    await loadApp();
  } catch (error) {
    showError(loginError, error.message);
  } finally {
    submit.disabled = false;
    submit.textContent = "Sign in";
  }
});

document.getElementById("logout").addEventListener("click", signOut);

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
  try {
    const wasEdit = Boolean(editingId);
    if (editingId) {
      await api(`/v1/transactions/${editingId}`, { method: "PUT", body: JSON.stringify(body) });
    } else {
      await api("/v1/transactions", { method: "POST", body: JSON.stringify(body) });
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
  } catch (error) {
    showError(txnError, error.message);
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

    await api(`/v1/transactions/${pendingDelete.id}`, { method: "DELETE" });
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
    await api("/v1/wallets/opening", {
      method: "PUT",
      body: JSON.stringify({
        cashOnHand: Number(document.getElementById("open-cash").value),
        gcash: Number(document.getElementById("open-gcash").value),
        tpc: Number(document.getElementById("open-tpc").value)
      })
    });
    await refreshLedger();
  } catch (error) {
    showError(openingError, error.message);
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
        role: document.getElementById("account-role").value
      })
    });
    accountForm.reset();
    showSettingsScreen("users");
  } catch (error) {
    showError(errorEl, error.message);
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
  const select = event.target.closest("select[data-user-id]");
  if (!select) {
    return;
  }

  const errorEl = document.getElementById("users-error");
  showError(errorEl, "");
  try {
    await api(`/v1/users/${select.dataset.userId}/role`, {
      method: "PATCH",
      body: JSON.stringify({ role: select.value })
    });
    await loadUsers();
  } catch (error) {
    showError(errorEl, error.message);
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

if (token()) {
  loadApp().catch(signOut);
}
