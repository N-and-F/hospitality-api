(() => {
  const api = (...args) => window.hospitalityApi(...args);
  const peso = (value) => window.hospitalityPeso(value);
  const escapeHtml = (value) => window.hospitalityEscapeHtml(value);
  const view = document.getElementById("felogan-view");
  const form = document.getElementById("felogan-serving-form");
  const servingError = document.getElementById("felogan-serving-error");
  const historyDate = document.getElementById("felogan-history-date");

  let rooms = [];
  let cooks = [];
  let menuItems = [];
  let servings = [];
  let purchases = [];
  let historyRange = "day";
  let purchaseRange = "day";
  let editingServingId = null;
  const editingCatalog = { rooms: null, cooks: null, "menu-items": null };

  function manilaDate() {
    return new Date().toLocaleDateString("en-CA", { timeZone: "Asia/Manila" });
  }

  function showError(element, message) {
    element.hidden = !message;
    element.textContent = message || "";
  }

  function option(item) {
    return `<option value="${item.id}">${escapeHtml(item.name)}</option>`;
  }

  function renderServingFields(extraItems = []) {
    const selectedRoom = document.getElementById("felogan-room").value;
    const selectedCook = document.getElementById("felogan-cook").value;
    document.getElementById("felogan-room").innerHTML = rooms.map(option).join("");
    document.getElementById("felogan-cook").innerHTML = cooks.map(option).join("");
    document.getElementById("felogan-room").value = selectedRoom;
    document.getElementById("felogan-cook").value = selectedCook;

    const itemsById = new Map(menuItems.map((item) => [item.id, item]));
    extraItems.forEach((item) => {
      const current = itemsById.get(item.id) || {};
      itemsById.set(item.id, { ...current, ...item });
    });
    const allItems = [...itemsById.values()].filter((item) => item.kind === "plate" || item.kind === "extra");
    const groups = [
      ["plate", "Plates"],
      ["extra", "Extras"]
    ];
    document.getElementById("felogan-menu-fields").innerHTML = groups.map(([kind, label]) => {
      const items = allItems.filter((item) => item.kind === kind);
      if (!items.length) {
        return "";
      }
      return `<fieldset>
        <legend>${label}</legend>
        <div class="quantity-grid">${items.map((item) => `
          <label>${escapeHtml(item.name)}
            <span>${peso(item.sellPrice)}${Number.isInteger(item.remainingPacks) ? ` · ${item.remainingPacks} left` : ""}</span>
            <input type="number" min="0" step="1" value="0" data-menu-qty="${item.id}" data-sell="${item.sellPrice}" data-cost="${item.cost ?? 0}">
          </label>`).join("")}</div>
      </fieldset>`;
    }).join("");
    updateSettlement();
  }

  function roundMoney(value) {
    return Math.round((Number(value) + Number.EPSILON) * 100) / 100;
  }

  function clientSettlement() {
    const lines = [...document.querySelectorAll("[data-menu-qty]")].map((input) => ({
      qty: Number(input.value) || 0,
      sellPrice: Number(input.dataset.sell) || 0,
      cost: Number(input.dataset.cost) || 0
    }));
    const qty = lines.reduce((sum, line) => sum + line.qty, 0);
    const sell = roundMoney(lines.reduce((sum, line) => sum + line.qty * line.sellPrice, 0));
    const cost = roundMoney(lines.reduce((sum, line) => sum + line.qty * line.cost, 0));
    const profit = roundMoney(sell - cost);
    const cook = roundMoney(profit * 0.4);
    const sham = roundMoney(profit * 0.1);
    const nf = roundMoney(profit - cook - sham);
    return { qty, sell, cost, profit, sham, cook, nf, house: roundMoney(nf + cost) };
  }

  function renderSettlement(totals = clientSettlement()) {
    const element = document.getElementById("felogan-settlement");
    if (!totals || !totals.qty) {
      element.hidden = true;
      element.innerHTML = "";
      return;
    }
    element.hidden = false;
    element.innerHTML = [
      ["Sell", totals.sell],
      ["Cost", totals.cost],
      ["Profit", totals.profit],
      ["Sham", totals.sham],
      ["Cook", totals.cook],
      ["N&F", totals.nf],
      ["House", totals.house]
    ].map(([label, value]) => `<div><span>${label}</span><strong>${peso(value)}</strong></div>`).join("");
  }

  function updateSettlement(totals) {
    renderSettlement(totals);
  }

  async function loadServingCatalogs() {
    const [roomList, cookList, menuList] = await Promise.all([
      api("/v1/felogan/rooms"),
      api("/v1/felogan/cooks"),
      api("/v1/felogan/menu-items")
    ]);
    rooms = roomList.items || [];
    cooks = cookList.items || [];
    menuItems = menuList.items || [];
    renderServingFields();
  }

  function showPanel(name) {
    document.querySelectorAll("[data-felogan-panel]").forEach((panel) => {
      panel.hidden = panel.dataset.feloganPanel !== name;
    });
    document.querySelectorAll("[data-felogan-nav]").forEach((button) => {
      button.classList.toggle("active", button.dataset.feloganNav === name);
    });
    if (name === "history") {
      refreshHistory().catch((error) => showHistoryError(error.message));
    }
    if (name === "catalog") {
      refreshCatalogs().catch((error) => showError(document.getElementById("felogan-menu-error"), error.message));
    }
    if (name === "stock") {
      refreshStock().catch((error) => showError(document.getElementById("felogan-purchase-error"), error.message));
    }
  }

  function servingSummary(lines) {
    const rank = (kind) => (kind === "plate" ? 0 : kind === "extra" ? 1 : 2);
    return [...lines]
      .sort((a, b) => rank(a.kind) - rank(b.kind))
      .map((item) => `${item.qty}× ${escapeHtml(item.name)}`)
      .join("<br>");
  }

  function showHistoryError(message) {
    const summary = document.getElementById("felogan-history-summary");
    summary.className = "history-summary-error";
    summary.textContent = message;
  }

  async function refreshHistory() {
    const data = await api(`/v1/felogan/servings?date=${historyDate.value || manilaDate()}&range=${historyRange}`);
    servings = data.items || [];
    const summary = document.getElementById("felogan-history-summary");
    summary.className = "history-summary";
    summary.innerHTML = `
      <div><span>Servings</span><strong>${data.totals.count}</strong></div>
      <div><span>Items</span><strong>${data.totals.qty}</strong></div>
      <div><span>Sell</span><strong>${peso(data.totals.sell)}</strong></div>
      <div><span>Profit</span><strong>${peso(data.totals.profit)}</strong></div>
      <div><span>Cook</span><strong>${peso(data.totals.cook)}</strong></div>`;
    document.getElementById("felogan-history-list").innerHTML = servings.length
      ? `<table class="history-table">
          <thead><tr><th>Date</th><th>Room</th><th>Cook</th><th>Summary</th><th>Sell</th><th>Profit</th><th>Cook</th><th>Paid cook</th><th>Note</th><th>Edit</th><th>Delete</th></tr></thead>
          <tbody>${servings.map((serving) => `<tr>
            <td data-label="Date">${escapeHtml(serving.servedOn)}</td>
            <td data-label="Room">${escapeHtml(serving.room.name)}</td>
            <td data-label="Cook">${escapeHtml(serving.cook.name)}</td>
            <td data-label="Summary">${servingSummary(serving.lines)}</td>
            <td data-label="Sell" class="num">${peso(serving.totals.sell)}</td>
            <td data-label="Profit" class="num">${peso(serving.totals.profit)}</td>
            <td data-label="Cook share" class="num">${peso(serving.totals.cook)}</td>
            <td data-label="Paid cook">${escapeHtml(serving.paidCook)}</td>
            <td data-label="Note">${serving.note ? `<button type="button" class="text-link" data-serving-note="${serving.id}">View</button>` : "—"}</td>
            <td data-label="Edit"><button type="button" class="text-link" data-serving-edit="${serving.id}">Edit</button></td>
            <td data-label="Delete"><button type="button" class="text-link danger" data-serving-delete="${serving.id}">Delete</button></td>
          </tr>`).join("")}</tbody>
        </table>`
      : `<p class="muted history-empty">No servings for this period.</p>`;
  }

  function ensureOption(select, item) {
    if (![...select.options].some((candidate) => candidate.value === item.id)) {
      select.insertAdjacentHTML("beforeend", option(item));
    }
    select.value = item.id;
  }

  function startServingEdit(serving) {
    editingServingId = serving.id;
    document.getElementById("felogan-serving-heading").textContent = "Edit serving";
    document.getElementById("felogan-serving-save").textContent = "Save changes";
    document.getElementById("felogan-serving-cancel").hidden = false;
    document.getElementById("felogan-serving-date").value = serving.servedOn;
    renderServingFields(serving.lines.map((item) => ({
      id: item.menuItemId,
      name: item.name,
      kind: item.kind,
      sellPrice: item.sellPrice,
      cost: item.cost
    })));
    ensureOption(document.getElementById("felogan-room"), serving.room);
    ensureOption(document.getElementById("felogan-cook"), serving.cook);
    document.getElementById("felogan-note").value = serving.note || "";
    document.getElementById("felogan-additional-profit").value = serving.additionalProfit ?? 0;
    document.getElementById("felogan-excess-paid").value = serving.excessPaid ?? 0;
    document.getElementById("felogan-paid-cook").value = serving.paidCook || "no";
    document.getElementById("felogan-paid-sham").value = serving.paidSham || "na";
    serving.lines.forEach((item) => {
      const input = document.querySelector(`[data-menu-qty="${item.menuItemId}"]`);
      if (input) {
        input.value = item.qty;
      }
    });
    updateSettlement(serving.totals);
    showPanel("serving");
  }

  function resetServingForm() {
    editingServingId = null;
    form.reset();
    document.getElementById("felogan-serving-heading").textContent = "New serving";
    document.getElementById("felogan-serving-save").textContent = "Save serving";
    document.getElementById("felogan-serving-cancel").hidden = true;
    document.getElementById("felogan-serving-date").value = manilaDate();
    renderServingFields();
    showError(servingError, "");
    updateSettlement();
  }

  function catalogConfig(type) {
    if (type === "rooms") {
      return { form: "felogan-room-form", name: "felogan-room-name", list: "felogan-rooms-list", error: "felogan-rooms-error" };
    }
    if (type === "cooks") {
      return { form: "felogan-cook-form", name: "felogan-cook-name", list: "felogan-cooks-list", error: "felogan-cooks-error" };
    }
    return { form: "felogan-menu-form", name: "felogan-menu-name", list: "felogan-menu-list", error: "felogan-menu-error" };
  }

  function catalogItemHtml(type, item) {
    const archived = Boolean(item.archivedAt);
    const details = type === "menu-items"
      ? `<small>${escapeHtml(item.kind)} · Sell ${peso(item.sellPrice)} · Cost ${peso(item.cost)} · ${item.remainingPacks} left</small>`
      : "";
    return `<div class="item ${archived ? "archived" : ""}">
      <b>${escapeHtml(item.name)}${archived ? " · Archived" : ""}</b>
      ${details}
      <div class="item-actions">
        <button type="button" class="text-link" data-catalog-edit="${type}" data-id="${item.id}">Edit</button>
        <button type="button" class="text-link ${archived ? "" : "danger"}" data-catalog-toggle="${type}" data-id="${item.id}" data-archived="${archived}">
          ${archived ? "Unarchive" : "Archive"}
        </button>
      </div>
    </div>`;
  }

  async function refreshCatalogs() {
    const [roomList, cookList, menuList] = await Promise.all([
      api("/v1/felogan/rooms?includeArchived=true"),
      api("/v1/felogan/cooks?includeArchived=true"),
      api("/v1/felogan/menu-items?includeArchived=true")
    ]);
    const catalogs = { rooms: roomList.items || [], cooks: cookList.items || [], "menu-items": menuList.items || [] };
    Object.entries(catalogs).forEach(([type, items]) => {
      document.getElementById(catalogConfig(type).list).innerHTML = items.map((item) => catalogItemHtml(type, item)).join("");
    });
    await loadServingCatalogs();
  }

  function resetCatalogForm(type) {
    const config = catalogConfig(type);
    document.getElementById(config.form).reset();
    editingCatalog[type] = null;
    const submit = document.querySelector(`#${config.form} button[type="submit"]`);
    submit.textContent = type === "menu-items" ? "Add menu item" : `Add ${type === "rooms" ? "room" : "cook"}`;
    document.querySelector(`[data-catalog-cancel="${type}"]`).hidden = true;
    showError(document.getElementById(config.error), "");
  }

  function startCatalogEdit(type, item) {
    const config = catalogConfig(type);
    editingCatalog[type] = item.id;
    document.getElementById(config.name).value = item.name;
    if (type === "menu-items") {
      document.getElementById("felogan-menu-kind").value = item.kind;
      document.getElementById("felogan-menu-sell").value = item.sellPrice;
      document.getElementById("felogan-menu-cost").value = item.cost;
    }
    document.querySelector(`#${config.form} button[type="submit"]`).textContent = "Save changes";
    document.querySelector(`[data-catalog-cancel="${type}"]`).hidden = false;
  }

  async function submitCatalog(type) {
    const config = catalogConfig(type);
    const body = { name: document.getElementById(config.name).value.trim() };
    if (type === "menu-items") {
      body.kind = document.getElementById("felogan-menu-kind").value;
      body.sellPrice = Number(document.getElementById("felogan-menu-sell").value);
      body.cost = Number(document.getElementById("felogan-menu-cost").value);
    }
    const id = editingCatalog[type];
    await api(`/v1/felogan/${type}${id ? `/${id}` : ""}`, {
      method: id ? "PUT" : "POST",
      body: JSON.stringify(body)
    });
    resetCatalogForm(type);
    await refreshCatalogs();
  }

  form.addEventListener("input", () => updateSettlement());

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    const submit = document.getElementById("felogan-serving-save");
    if (submit.disabled) {
      return;
    }
    submit.disabled = true;
    showError(servingError, "");
    const lines = [...document.querySelectorAll("[data-menu-qty]")]
      .map((input) => ({ menuItemId: input.dataset.menuQty, qty: Number(input.value) }))
      .filter((item) => item.qty > 0);
    const body = {
      servedOn: document.getElementById("felogan-serving-date").value,
      roomId: document.getElementById("felogan-room").value,
      cookId: document.getElementById("felogan-cook").value,
      note: document.getElementById("felogan-note").value.trim(),
      additionalProfit: Number(document.getElementById("felogan-additional-profit").value || 0),
      excessPaid: Number(document.getElementById("felogan-excess-paid").value || 0),
      paidCook: document.getElementById("felogan-paid-cook").value,
      paidSham: document.getElementById("felogan-paid-sham").value,
      lines
    };
    if (!editingServingId) {
      body.clientRequestId = crypto.randomUUID();
    }
    try {
      const saved = await api(`/v1/felogan/servings${editingServingId ? `/${editingServingId}` : ""}`, {
        method: editingServingId ? "PUT" : "POST",
        body: JSON.stringify(body)
      });
      updateSettlement(saved.totals);
      resetServingForm();
      if (!document.getElementById("felogan-history").hidden) {
        await refreshHistory();
      }
    } catch (error) {
      showError(servingError, error.message);
    } finally {
      submit.disabled = false;
    }
  });

  document.getElementById("felogan-serving-cancel").addEventListener("click", () => {
    resetServingForm();
    showPanel("history");
  });

  document.querySelectorAll("[data-felogan-nav]").forEach((button) => {
    button.addEventListener("click", () => {
      if (button.dataset.feloganNav === "serving" && editingServingId) {
        resetServingForm();
      }
      showPanel(button.dataset.feloganNav);
    });
  });

  document.querySelectorAll("[data-felogan-range]").forEach((button) => {
    button.addEventListener("click", () => {
      historyRange = button.dataset.feloganRange;
      document.querySelectorAll("[data-felogan-range]").forEach((tab) => tab.classList.toggle("active", tab === button));
      refreshHistory().catch((error) => showHistoryError(error.message));
    });
  });

  historyDate.addEventListener("change", () => refreshHistory().catch((error) => showHistoryError(error.message)));

  document.getElementById("felogan-history-list").addEventListener("click", (event) => {
    const note = event.target.closest("[data-serving-note]");
    const edit = event.target.closest("[data-serving-edit]");
    const remove = event.target.closest("[data-serving-delete]");
    const id = note?.dataset.servingNote || edit?.dataset.servingEdit || remove?.dataset.servingDelete;
    const serving = servings.find((item) => item.id === id);
    if (note && serving?.note) {
      window.openHospitalityNote(serving.note);
    } else if (edit && serving) {
      startServingEdit(serving);
    } else if (remove && serving) {
      window.openHospitalityDelete({
        kind: "serving",
        id: serving.id,
        title: "Delete serving",
        copy: `Delete ${serving.room.name} serving? This cannot be undone.`
      });
    }
  });

  ["rooms", "cooks", "menu-items"].forEach((type) => {
    const config = catalogConfig(type);
    document.getElementById(config.form).addEventListener("submit", (event) => {
      event.preventDefault();
      showError(document.getElementById(config.error), "");
      submitCatalog(type).catch((error) => showError(document.getElementById(config.error), error.message));
    });
  });

  document.querySelectorAll("[data-catalog-cancel]").forEach((button) => {
    button.addEventListener("click", () => resetCatalogForm(button.dataset.catalogCancel));
  });

  document.getElementById("felogan-catalog").addEventListener("click", async (event) => {
    const edit = event.target.closest("[data-catalog-edit]");
    const toggle = event.target.closest("[data-catalog-toggle]");
    if (!edit && !toggle) {
      return;
    }
    const control = edit || toggle;
    const type = edit?.dataset.catalogEdit || toggle.dataset.catalogToggle;
    const config = catalogConfig(type);

    try {
      if (edit) {
        const data = await api(`/v1/felogan/${type}?includeArchived=true`);
        const selected = (data.items || []).find((candidate) => candidate.id === control.dataset.id);
        if (selected) startCatalogEdit(type, selected);
        return;
      }

      const action = toggle.dataset.archived === "true" ? "unarchive" : "archive";
      await api(`/v1/felogan/${type}/${control.dataset.id}/${action}`, { method: "POST" });
      await refreshCatalogs();
    } catch (error) {
      showError(document.getElementById(config.error), error.message);
    }
  });

  window.deleteFeloganServing = async (id) => {
    await api(`/v1/felogan/servings/${id}`, { method: "DELETE" });
    refreshHistory().catch((error) => showHistoryError(error.message));
  };

  function showPurchaseHistoryError(message) {
    const summary = document.getElementById("felogan-purchase-history-summary");
    summary.className = "history-summary-error";
    summary.textContent = message;
  }

  function renderPurchaseItemOptions() {
    const select = document.getElementById("felogan-purchase-item");
    const selected = select.value;
    select.innerHTML = menuItems.map((item) =>
      `<option value="${item.id}">${escapeHtml(item.name)} (${escapeHtml(item.kind)})</option>`).join("");
    if ([...select.options].some((candidate) => candidate.value === selected)) {
      select.value = selected;
    }
  }

  async function refreshStock() {
    await loadServingCatalogs();
    renderPurchaseItemOptions();
    document.getElementById("felogan-stock-remaining").innerHTML = menuItems.length
      ? `<table class="history-table">
          <thead><tr><th>Item</th><th>Kind</th><th>Remaining</th></tr></thead>
          <tbody>${menuItems.map((item) => `<tr>
            <td data-label="Item">${escapeHtml(item.name)}</td>
            <td data-label="Kind">${escapeHtml(item.kind)}</td>
            <td data-label="Remaining" class="num">${item.remainingPacks}</td>
          </tr>`).join("")}</tbody>
        </table>`
      : `<p class="muted history-empty">No catalog items.</p>`;
    await refreshPurchaseHistory();
  }

  async function refreshPurchaseHistory() {
    const date = document.getElementById("felogan-purchase-history-date").value || manilaDate();
    const data = await api(`/v1/felogan/purchases?date=${date}&range=${purchaseRange}`);
    purchases = data.items || [];
    const summary = document.getElementById("felogan-purchase-history-summary");
    summary.className = "history-summary";
    summary.innerHTML = `
      <div><span>Purchases</span><strong>${data.totals.count}</strong></div>
      <div><span>Qty</span><strong>${data.totals.qty}</strong></div>`;
    document.getElementById("felogan-purchase-history-list").innerHTML = purchases.length
      ? `<table class="history-table">
          <thead><tr><th>Date</th><th>Item</th><th>Qty</th><th>Note</th><th>Delete</th></tr></thead>
          <tbody>${purchases.map((purchase) => `<tr>
            <td data-label="Date">${escapeHtml(purchase.purchasedOn)}</td>
            <td data-label="Item">${escapeHtml(purchase.item.name)}</td>
            <td data-label="Qty" class="num">${purchase.qty}</td>
            <td data-label="Note">${purchase.note ? `<button type="button" class="text-link" data-purchase-note="${purchase.id}">View</button>` : "—"}</td>
            <td data-label="Delete"><button type="button" class="text-link danger" data-purchase-delete="${purchase.id}">Delete</button></td>
          </tr>`).join("")}</tbody>
        </table>`
      : `<p class="muted history-empty">No purchases for this period.</p>`;
  }

  window.deleteFeloganPurchase = async (id) => {
    await api(`/v1/felogan/purchases/${id}`, { method: "DELETE" });
    refreshStock().catch((error) => showError(document.getElementById("felogan-purchase-error"), error.message));
  };

  document.getElementById("felogan-purchase-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    const submit = document.getElementById("felogan-purchase-save");
    if (submit.disabled) {
      return;
    }
    submit.disabled = true;
    showError(document.getElementById("felogan-purchase-error"), "");
    try {
      await api("/v1/felogan/purchases", {
        method: "POST",
        body: JSON.stringify({
          purchasedOn: document.getElementById("felogan-purchase-date").value,
          menuItemId: document.getElementById("felogan-purchase-item").value,
          qty: Number(document.getElementById("felogan-purchase-qty").value),
          note: document.getElementById("felogan-purchase-note").value.trim(),
          clientRequestId: crypto.randomUUID()
        })
      });
      document.getElementById("felogan-purchase-form").reset();
      document.getElementById("felogan-purchase-date").value = manilaDate();
      await refreshStock();
    } catch (error) {
      showError(document.getElementById("felogan-purchase-error"), error.message);
    } finally {
      submit.disabled = false;
    }
  });

  document.querySelectorAll("[data-felogan-purchase-range]").forEach((button) => {
    button.addEventListener("click", () => {
      purchaseRange = button.dataset.feloganPurchaseRange;
      document.querySelectorAll("[data-felogan-purchase-range]").forEach((tab) => tab.classList.toggle("active", tab === button));
      refreshPurchaseHistory().catch((error) => showPurchaseHistoryError(error.message));
    });
  });

  document.getElementById("felogan-purchase-history-date").addEventListener("change", () => {
    refreshPurchaseHistory().catch((error) => showPurchaseHistoryError(error.message));
  });

  document.getElementById("felogan-purchase-history-list").addEventListener("click", (event) => {
    const note = event.target.closest("[data-purchase-note]");
    const remove = event.target.closest("[data-purchase-delete]");
    const id = note?.dataset.purchaseNote || remove?.dataset.purchaseDelete;
    const purchase = purchases.find((item) => item.id === id);
    if (note && purchase?.note) {
      window.openHospitalityNote(purchase.note);
    } else if (remove && purchase) {
      window.openHospitalityDelete({
        kind: "purchase",
        id: purchase.id,
        title: "Delete purchase",
        copy: `Delete ${purchase.qty}× ${purchase.item.name}? This cannot be undone.`
      });
    }
  });

  window.openFeloganApp = async () => {
    const user = window.hospitalityCurrentUser();
    document.getElementById("felogan-who").textContent = `${user.username} · ${(user.roles || []).join(", ")}`;
    document.getElementById("login-view").hidden = true;
    document.getElementById("chooser-view").hidden = true;
    document.getElementById("app-view").hidden = true;
    view.hidden = false;
    historyDate.value = manilaDate();
    document.getElementById("felogan-serving-date").value = manilaDate();
    document.getElementById("felogan-purchase-date").value = manilaDate();
    document.getElementById("felogan-purchase-history-date").value = manilaDate();
    await loadServingCatalogs();
    resetServingForm();
    showPanel("serving");
  };

  window.bootHospitality();
})();
