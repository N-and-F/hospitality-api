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
  let editingServingId = null;
  let editingExtras = null;
  let cookPercent = 40;
  let catalogType = "";
  const editingCatalog = { rooms: null, cooks: null, "menu-items": null };

  function manilaDate() {
    return new Date().toLocaleDateString("en-CA", { timeZone: "Asia/Manila" });
  }

  function showError(element, message) {
    element.hidden = !message;
    element.textContent = message || "";
  }

  function showToast(message, kind) {
    window.hospitalityToast(message, kind);
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
      ["plate", "Silog"],
      ["extra", "Extras"]
    ];
    const fields = document.getElementById("felogan-menu-fields");
    const note = document.getElementById("felogan-serving-note");
    if (note && fields.contains(note)) {
      fields.after(note);
    }
    fields.innerHTML = groups.map(([kind, label]) => {
      const items = allItems.filter((item) => item.kind === kind);
      if (!items.length) {
        return "";
      }
      return `<fieldset data-serving-group="${kind}">
        <legend>${label}</legend>
        <div class="serving-lines">${items.map((item) => `
          <div class="serving-line">
            <span class="serving-line-name">${escapeHtml(item.name)}</span>
            <div class="stepper">
              <button type="button" data-step="-1" aria-label="Decrease ${escapeHtml(item.name)}">−</button>
              <input type="number" min="0" step="1" value="0" inputmode="numeric" data-menu-qty="${item.id}" data-sell="${item.sellPrice}" data-cost="${item.cost ?? 0}" aria-label="${escapeHtml(item.name)} quantity">
              <button type="button" data-step="1" aria-label="Increase ${escapeHtml(item.name)}">+</button>
            </div>
          </div>`).join("")}</div>
      </fieldset>`;
    }).join("");
    const plates = document.querySelector("[data-serving-group='plate']");
    if (plates && note) {
      plates.appendChild(note);
    }
    updateTotal();
  }

  function roundMoney(value) {
    return Math.round((Number(value) + Number.EPSILON) * 100) / 100;
  }

  function servingSellTotal() {
    return roundMoney([...document.querySelectorAll("[data-menu-qty]")].reduce(
      (sum, input) => sum + (Number(input.value) || 0) * (Number(input.dataset.sell) || 0),
      0
    ));
  }

  function updateTotal() {
    document.getElementById("felogan-serving-total").textContent = peso(servingSellTotal());
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
      showCatalogSection("");
      refreshCatalogs().catch((error) => showError(document.getElementById("felogan-settings-error"), error.message));
    }
    if (name === "stock") {
      refreshStock().catch((error) => showError(document.getElementById("felogan-purchase-error"), error.message));
    }
  }

  function servingUpdateBody(serving, extras = {}) {
    return {
      servedOn: serving.servedOn,
      roomId: serving.room.id,
      cookId: serving.cook.id,
      note: serving.note || "",
      lines: serving.lines.map((item) => ({ menuItemId: item.menuItemId, qty: item.qty })),
      additionalProfit: serving.additionalProfit ?? 0,
      excessPaid: serving.excessPaid ?? 0,
      paidCook: serving.paidCook || "no",
      ...extras
    };
  }

  function paidFlagLabel(value) {
    return value === "na" ? "N/A" : value;
  }

  function paidCookCellHtml(serving) {
    return `<span class="paid-cook-cell">
      <span>${escapeHtml(paidFlagLabel(serving.paidCook))}</span>
      <button type="button" class="icon-btn" data-paid-cook-edit="${serving.id}" aria-label="Edit paid cook">
        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 19l1.4-4.5L16.6 4.3a1.5 1.5 0 0 1 2.1 0l1 1a1.5 1.5 0 0 1 0 2.1L9.5 17.6 5 19Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M14 6.2 17.8 10" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>
      </button>
    </span>`;
  }

  function startPaidCookEdit(cell, serving) {
    const current = serving.paidCook || "no";
    cell.innerHTML = `<select data-paid-cook="${serving.id}" aria-label="Paid cook">${
      ["yes", "na", "no"].map((value) => `<option value="${value}"${value === current ? " selected" : ""}>${paidFlagLabel(value)}</option>`).join("")
    }</select>`;
    cell.querySelector("select").focus();
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
            <td data-label="Paid cook">${paidCookCellHtml(serving)}</td>
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
    editingExtras = {
      additionalProfit: serving.additionalProfit ?? 0,
      excessPaid: serving.excessPaid ?? 0,
      paidCook: serving.paidCook || "no"
    };
    serving.lines.forEach((item) => {
      const input = document.querySelector(`[data-menu-qty="${item.menuItemId}"]`);
      if (input) {
        input.value = item.qty;
      }
    });
    updateTotal();
    showError(servingError, "");
    showPanel("serving");
  }

  function resetServingForm() {
    editingServingId = null;
    editingExtras = null;
    form.reset();
    document.getElementById("felogan-serving-heading").textContent = "New serving";
    document.getElementById("felogan-serving-save").textContent = "Save serving";
    document.getElementById("felogan-serving-cancel").hidden = true;
    document.getElementById("felogan-serving-date").value = manilaDate();
    renderServingFields();
    showError(servingError, "");
    updateTotal();
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

  function kindLabel(kind) {
    if (kind === "plate") return "Silog";
    if (kind === "extra") return "Extra";
    if (kind === "stock") return "Stock";
    return kind || "";
  }

  function catalogItemHtml(type, item) {
    const archived = Boolean(item.archivedAt);
    return `<div class="item ${archived ? "archived" : ""}">
      <b>${escapeHtml(item.name)}${archived ? " · Archived" : ""}</b>
      <div class="item-actions">
        <button type="button" class="text-link" data-catalog-edit="${type}" data-id="${item.id}">Edit</button>
        <button type="button" class="text-link ${archived ? "" : "danger"}" data-catalog-toggle="${type}" data-id="${item.id}" data-archived="${archived}">
          ${archived ? "Unarchive" : "Archive"}
        </button>
      </div>
    </div>`;
  }

  function menuTableHtml(items) {
    if (!items.length) {
      return `<p class="muted history-empty">No items.</p>`;
    }
    return `<table class="history-table">
      <thead><tr><th>Name</th><th>Kind</th><th>Sell</th><th>Cost</th><th>Profit</th><th>Cook ${cookPercent}%</th><th>Left</th><th>Edit</th><th>Archive</th></tr></thead>
      <tbody>${items.map((item) => {
        const archived = Boolean(item.archivedAt);
        return `<tr class="${archived ? "archived" : ""}">
          <td data-label="Name">${escapeHtml(item.name)}${archived ? " · Archived" : ""}</td>
          <td data-label="Kind">${escapeHtml(kindLabel(item.kind))}</td>
          <td data-label="Sell" class="num">${peso(item.sellPrice)}</td>
          <td data-label="Cost" class="num">${peso(item.cost)}</td>
          <td data-label="Profit" class="num">${peso(item.profit)}</td>
          <td data-label="Cook ${cookPercent}%" class="num">${peso(item.cook)}</td>
          <td data-label="Left" class="num">${item.remainingPacks}</td>
          <td data-label="Edit"><button type="button" class="text-link" data-catalog-edit="menu-items" data-id="${item.id}">Edit</button></td>
          <td data-label="Archive" class="actions"><div class="item-actions">
            <button type="button" class="text-link ${archived ? "" : "danger"}" data-catalog-toggle="menu-items" data-id="${item.id}" data-archived="${archived}">
              ${archived ? "Unarchive" : "Archive"}
            </button>
          </div></td>
        </tr>`;
      }).join("")}</tbody>
    </table>`;
  }

  async function refreshCatalogs() {
    const [roomList, cookList, menuList, settings] = await Promise.all([
      api("/v1/felogan/rooms?includeArchived=true"),
      api("/v1/felogan/cooks?includeArchived=true"),
      api("/v1/felogan/menu-items?includeArchived=true"),
      api("/v1/felogan/settings")
    ]);
    cookPercent = settings.cookPercent;
    document.getElementById("felogan-cook-percent").value = cookPercent;
    const catalogs = { rooms: roomList.items || [], cooks: cookList.items || [], "menu-items": menuList.items || [] };
    Object.entries(catalogs).forEach(([type, items]) => {
      document.getElementById(catalogConfig(type).list).innerHTML = type === "menu-items"
        ? menuTableHtml(items)
        : items.map((item) => catalogItemHtml(type, item)).join("");
    });
    await loadServingCatalogs();
  }

  function showCatalogSection(type) {
    if (catalogType && catalogType !== type) {
      resetCatalogForm(catalogType);
    }
    catalogType = type || "";
    const menu = document.querySelector("[data-felogan-settings-menu]");
    menu.hidden = Boolean(catalogType);
    document.querySelectorAll("[data-catalog-section]").forEach((section) => {
      section.hidden = section.dataset.catalogSection !== catalogType;
    });
  }

  function resetCatalogForm(type) {
    if (!type) {
      return;
    }
    const config = catalogConfig(type);
    document.getElementById(config.form).reset();
    editingCatalog[type] = null;
    const submit = document.querySelector(`#${config.form} button[type="submit"]`);
    submit.textContent = type === "menu-items" ? "Add menu item" : `Add ${type === "rooms" ? "room" : "cook"}`;
    document.querySelector(`[data-catalog-cancel="${type}"]`).hidden = true;
    showError(document.getElementById(config.error), "");
    if (type === "menu-items") {
      document.querySelector("#felogan-menu-kind option[value=\"stock\"]")?.remove();
    }
  }

  function startCatalogEdit(type, item) {
    const config = catalogConfig(type);
    editingCatalog[type] = item.id;
    document.getElementById(config.name).value = item.name;
    if (type === "menu-items") {
      const kind = document.getElementById("felogan-menu-kind");
      if (item.kind === "stock" && !kind.querySelector("option[value=\"stock\"]")) {
        kind.insertAdjacentHTML("beforeend", "<option value=\"stock\">Stock</option>");
      }
      kind.value = item.kind;
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
    const labels = { rooms: "Room", cooks: "Cook", "menu-items": "Menu item" };
    showToast(id ? `${labels[type]} updated.` : `${labels[type]} saved.`);
  }

  form.addEventListener("input", () => updateTotal());

  document.getElementById("felogan-menu-fields").addEventListener("click", (event) => {
    const button = event.target.closest("[data-step]");
    if (!button) {
      return;
    }
    const input = button.parentElement.querySelector("[data-menu-qty]");
    if (!input) {
      return;
    }
    input.value = Math.max(0, (Number(input.value) || 0) + Number(button.dataset.step));
    updateTotal();
  });

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
      lines
    };
    if (editingExtras) {
      Object.assign(body, editingExtras);
    }
    if (!editingServingId) {
      body.clientRequestId = crypto.randomUUID();
    }
    try {
      const savedEdit = Boolean(editingServingId);
      await api(`/v1/felogan/servings${editingServingId ? `/${editingServingId}` : ""}`, {
        method: editingServingId ? "PUT" : "POST",
        body: JSON.stringify(body)
      });
      resetServingForm();
      showToast(savedEdit ? "Serving updated." : "Serving saved.");
      if (!document.getElementById("felogan-history").hidden) {
        await refreshHistory();
      }
    } catch (error) {
      showError(servingError, error.message);
      showToast(error.message, "error");
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

  document.querySelectorAll("[data-felogan-catalog]").forEach((button) => {
    button.addEventListener("click", () => {
      showCatalogSection(button.dataset.feloganCatalog);
    });
  });

  document.querySelectorAll("[data-felogan-settings-back]").forEach((button) => {
    button.addEventListener("click", () => showCatalogSection(""));
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
    const paidEdit = event.target.closest("[data-paid-cook-edit]");
    const id = note?.dataset.servingNote
      || edit?.dataset.servingEdit
      || remove?.dataset.servingDelete
      || paidEdit?.dataset.paidCookEdit;
    const serving = servings.find((item) => item.id === id);
    if (note && serving?.note) {
      window.openHospitalityNote(serving.note);
    } else if (paidEdit && serving) {
      startPaidCookEdit(paidEdit.closest("td"), serving);
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

  document.getElementById("felogan-history-list").addEventListener("change", async (event) => {
    const select = event.target.closest("[data-paid-cook]");
    if (!select || select.disabled) {
      return;
    }
    const serving = servings.find((item) => item.id === select.dataset.paidCook);
    if (!serving || select.value === (serving.paidCook || "no")) {
      return;
    }
    select.disabled = true;
    try {
      await api(`/v1/felogan/servings/${serving.id}`, {
        method: "PUT",
        body: JSON.stringify(servingUpdateBody(serving, { paidCook: select.value }))
      });
      showToast("Serving updated.");
      await refreshHistory();
    } catch (error) {
      select.disabled = false;
      showHistoryError(error.message);
      showToast(error.message, "error");
    }
  });

  document.getElementById("felogan-history-list").addEventListener("focusout", (event) => {
    const select = event.target.closest("[data-paid-cook]");
    if (!select || select.disabled) {
      return;
    }
    const serving = servings.find((item) => item.id === select.dataset.paidCook);
    if (serving) {
      select.closest("td").innerHTML = paidCookCellHtml(serving);
    }
  });

  document.getElementById("felogan-cook-share-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    const shareError = document.getElementById("felogan-cook-share-error");
    showError(shareError, "");
    try {
      const settings = await api("/v1/felogan/settings", {
        method: "PUT",
        body: JSON.stringify({ cookPercent: Number(document.getElementById("felogan-cook-percent").value) })
      });
      cookPercent = settings.cookPercent;
      showToast("Cook share saved.");
      await refreshCatalogs();
    } catch (error) {
      showError(shareError, error.message);
      showToast(error.message, "error");
    }
  });

  ["rooms", "cooks", "menu-items"].forEach((type) => {
    const config = catalogConfig(type);
    document.getElementById(config.form).addEventListener("submit", (event) => {
      event.preventDefault();
      showError(document.getElementById(config.error), "");
      submitCatalog(type).catch((error) => {
        showError(document.getElementById(config.error), error.message);
        showToast(error.message, "error");
      });
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
    const list = document.getElementById("felogan-purchase-history-list");
    list.innerHTML = `<p class="error">${escapeHtml(message)}</p>`;
  }

  function extraItems() {
    return menuItems.filter((item) => item.kind === "extra");
  }

  function renderPurchaseItemOptions() {
    const select = document.getElementById("felogan-purchase-item");
    const selected = select.value;
    select.innerHTML = extraItems().map(option).join("");
    if ([...select.options].some((candidate) => candidate.value === selected)) {
      select.value = selected;
    }
  }

  async function refreshStock() {
    await loadServingCatalogs();
    renderPurchaseItemOptions();
    const extras = extraItems();
    document.getElementById("felogan-stock-remaining").innerHTML = extras.length
      ? `<table class="history-table">
          <thead><tr><th>Item</th><th>Remaining</th></tr></thead>
          <tbody>${extras.map((item) => `<tr>
            <td data-label="Item">${escapeHtml(item.name)}</td>
            <td data-label="Remaining" class="num">${item.remainingPacks}</td>
          </tr>`).join("")}</tbody>
        </table>`
      : `<p class="muted history-empty">No extras.</p>`;
    await refreshPurchaseHistory();
  }

  async function refreshPurchaseHistory() {
    const date = document.getElementById("felogan-purchase-history-date").value || manilaDate();
    const data = await api(`/v1/felogan/purchases?date=${date}&range=day`);
    purchases = data.items || [];
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
      : `<p class="muted history-empty">No purchases for this date.</p>`;
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
      showToast("Purchase saved.");
    } catch (error) {
      showError(document.getElementById("felogan-purchase-error"), error.message);
      showToast(error.message, "error");
    } finally {
      submit.disabled = false;
    }
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
