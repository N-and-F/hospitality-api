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
  let historyRange = "day";
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

    const allItems = [...menuItems];
    extraItems.forEach((item) => {
      if (!allItems.some((existing) => existing.id === item.id)) {
        allItems.push(item);
      }
    });
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
            <span>${peso(item.sellPrice)}</span>
            <input type="number" min="0" step="1" value="0" data-menu-qty="${item.id}">
          </label>`).join("")}</div>
      </fieldset>`;
    }).join("");
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
      <div><span>Sell</span><strong>${peso(data.totals.sell)}</strong></div>`;
    document.getElementById("felogan-history-list").innerHTML = servings.length
      ? `<table class="history-table">
          <thead><tr><th>Date</th><th>Room</th><th>Cook</th><th>Summary</th><th>Sell</th><th>Note</th><th>Edit</th><th>Delete</th></tr></thead>
          <tbody>${servings.map((serving) => `<tr>
            <td data-label="Date">${escapeHtml(serving.servedOn)}</td>
            <td data-label="Room">${escapeHtml(serving.room.name)}</td>
            <td data-label="Cook">${escapeHtml(serving.cook.name)}</td>
            <td data-label="Summary">${serving.lines.map((item) => `${item.qty}× ${escapeHtml(item.name)}`).join(", ")}</td>
            <td data-label="Sell" class="num">${peso(serving.totals.sell)}</td>
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
      sellPrice: item.sellPrice
    })));
    ensureOption(document.getElementById("felogan-room"), serving.room);
    ensureOption(document.getElementById("felogan-cook"), serving.cook);
    document.getElementById("felogan-note").value = serving.note || "";
    serving.lines.forEach((item) => {
      const input = document.querySelector(`[data-menu-qty="${item.menuItemId}"]`);
      if (input) {
        input.value = item.qty;
      }
    });
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
      ? `<small>${escapeHtml(item.kind)} · Sell ${peso(item.sellPrice)} · Cost ${peso(item.cost)}</small>`
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

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
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
    if (!editingServingId) {
      body.clientRequestId = crypto.randomUUID();
    }
    try {
      await api(`/v1/felogan/servings${editingServingId ? `/${editingServingId}` : ""}`, {
        method: editingServingId ? "PUT" : "POST",
        body: JSON.stringify(body)
      });
      resetServingForm();
      if (!document.getElementById("felogan-history").hidden) {
        await refreshHistory();
      }
    } catch (error) {
      showError(servingError, error.message);
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
    await refreshHistory();
  };

  window.openFeloganApp = async () => {
    const user = window.hospitalityCurrentUser();
    document.getElementById("felogan-who").textContent = `${user.username} · ${(user.roles || []).join(", ")}`;
    document.getElementById("login-view").hidden = true;
    document.getElementById("chooser-view").hidden = true;
    document.getElementById("app-view").hidden = true;
    view.hidden = false;
    historyDate.value = manilaDate();
    document.getElementById("felogan-serving-date").value = manilaDate();
    await loadServingCatalogs();
    resetServingForm();
    showPanel("serving");
  };

  window.bootHospitality();
})();
