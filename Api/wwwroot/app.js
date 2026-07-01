"use strict";

// ---------------------------------------------------------------------------
// Small helpers
// ---------------------------------------------------------------------------
const $ = (id) => document.getElementById(id);

function uuid() {
    if (crypto.randomUUID) return crypto.randomUUID();
    return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
        const r = (Math.random() * 16) | 0;
        const v = c === "x" ? r : (r & 0x3) | 0x8;
        return v.toString(16);
    });
}

function baseUrl() {
    const raw = $("baseUrl").value.trim() || window.location.origin + "/";
    return raw.endsWith("/") ? raw : raw + "/";
}

let toastTimer;
function toast(message, kind = "") {
    const el = $("toast");
    el.textContent = message;
    el.className = "toast show " + kind;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => (el.className = "toast " + kind), 3500);
}

// Thin fetch wrapper that normalizes the API's { error } payload on failure.
async function api(path, options = {}) {
    const res = await fetch(baseUrl() + path, {
        headers: { "Content-Type": "application/json", ...(options.headers || {}) },
        ...options,
    });

    const text = await res.text();
    const body = text ? JSON.parse(text) : null;

    if (!res.ok) {
        const msg = (body && body.error) || `HTTP ${res.status} (${res.statusText})`;
        throw new Error(msg);
    }
    return body;
}

function withSpinner(btn, label, fn) {
    const original = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = `<span class="spinner"></span>${label}`;
    return Promise.resolve()
        .then(fn)
        .finally(() => {
            btn.disabled = false;
            btn.innerHTML = original;
        });
}

// ---------------------------------------------------------------------------
// State
// ---------------------------------------------------------------------------
const state = {
    selectedProfessional: null,
    selectedSlot: null,
    currentHoldId: null,
};

// ---------------------------------------------------------------------------
// Discovery: specialties + units
// ---------------------------------------------------------------------------
async function loadSpecialties() {
    try {
        const items = await api("api/specialties");
        const sel = $("specialty");
        sel.innerHTML = "";
        items.forEach((s) => {
            const opt = document.createElement("option");
            opt.value = s.id;
            opt.textContent = s.name;
            sel.appendChild(opt);
        });
    } catch (e) {
        toast("Falha ao carregar especialidades: " + e.message, "err");
    }
}

async function loadUnits() {
    try {
        const items = await api("api/units");
        const sel = $("unit");
        sel.innerHTML = '<option value="">Todas</option>';
        items.forEach((u) => {
            const opt = document.createElement("option");
            opt.value = u.id;
            opt.textContent = u.name;
            sel.appendChild(opt);
        });
    } catch (e) {
        toast("Falha ao carregar unidades: " + e.message, "err");
    }
}

// ---------------------------------------------------------------------------
// Step 1 -> 2: search professionals
// ---------------------------------------------------------------------------
async function searchProfessionals() {
    const specialtyId = $("specialty").value;
    const date = $("date").value;
    const unitId = $("unit").value;

    if (!specialtyId) return toast("Selecione uma especialidade.", "err");
    if (!date) return toast("Selecione uma data.", "err");

    let path = `api/professionals?specialtyId=${specialtyId}&date=${date}`;
    if (unitId) path += `&unitId=${unitId}`;

    const items = await api(path);
    renderProfessionals(items);
}

function renderProfessionals(items) {
    const container = $("professionalsList");
    container.innerHTML = "";
    if (!items.length) {
        container.innerHTML = '<p class="empty">Nenhum profissional encontrado para os filtros.</p>';
        return;
    }
    items.forEach((p) => {
        const row = document.createElement("div");
        row.className = "row";
        row.innerHTML = `
            <div>
                <div class="title">${p.professionalName}</div>
                <div class="meta">${p.specialtyName} &middot; ${p.clinicUnitName}</div>
            </div>
            <span class="pill">selecionar</span>`;
        row.addEventListener("click", () => selectProfessional(p, row));
        container.appendChild(row);
    });
}

// ---------------------------------------------------------------------------
// Step 2 -> 3: availability for a professional
// ---------------------------------------------------------------------------
async function selectProfessional(professional, rowEl) {
    document.querySelectorAll("#professionalsList .row").forEach((r) => r.classList.remove("selected"));
    rowEl.classList.add("selected");
    state.selectedProfessional = professional;
    state.selectedSlot = null;
    state.currentHoldId = null;
    resetBookingPanel();

    const date = $("date").value;
    const period = $("period").value;
    // Availability is scoped to the professional's own unit.
    const unitId = professional.clinicUnitId;

    $("availabilityContext").textContent =
        `${professional.professionalName} — ${date} — ${$("period").selectedOptions[0].textContent} — ${professional.clinicUnitName}`;

    const slotsEl = $("slotsList");
    slotsEl.innerHTML = '<p class="empty"><span class="spinner"></span>Carregando horários...</p>';

    try {
        const path = `api/professionals/${professional.professionalId}/availability?date=${date}&period=${period}&unitId=${unitId}`;
        const slots = await api(path);
        renderSlots(slots);
    } catch (e) {
        slotsEl.innerHTML = `<p class="empty">Erro: ${e.message}</p>`;
        toast("Falha ao carregar horários: " + e.message, "err");
    }
}

function renderSlots(slots) {
    const container = $("slotsList");
    container.innerHTML = "";
    if (!slots.length) {
        container.innerHTML = '<p class="empty">Sem horários disponíveis neste período.</p>';
        return;
    }
    slots.forEach((slot) => {
        const start = new Date(slot.startsAt);
        const end = new Date(slot.endsAt);
        const row = document.createElement("div");
        row.className = "row";
        row.innerHTML = `
            <div>
                <div class="title">${fmtTime(start)} – ${fmtTime(end)}</div>
                <div class="meta">${fmtDate(start)}${slot.seatCode ? " &middot; assento " + slot.seatCode : ""}</div>
            </div>
            <span class="pill">reservar</span>`;
        row.addEventListener("click", () => selectSlot(slot, row));
        container.appendChild(row);
    });
}

// ---------------------------------------------------------------------------
// Step 3 -> 4: hold + confirm
// ---------------------------------------------------------------------------
function selectSlot(slot, rowEl) {
    document.querySelectorAll("#slotsList .row").forEach((r) => r.classList.remove("selected"));
    rowEl.classList.add("selected");
    state.selectedSlot = slot;
    state.currentHoldId = null;
    renderBookingPanel();
}

function renderBookingPanel() {
    const slot = state.selectedSlot;
    const start = new Date(slot.startsAt);
    const panel = $("bookingPanel");
    panel.innerHTML = `
        <dl class="booking-summary">
            <dt>Profissional</dt><dd>${state.selectedProfessional.professionalName}</dd>
            <dt>Horário</dt><dd>${fmtDate(start)} ${fmtTime(start)}</dd>
            <dt>Slot ID</dt><dd>${slot.slotId}</dd>
        </dl>
        <div class="actions">
            <button id="holdBtn" class="primary">Criar hold</button>
            <button id="confirmBtn" class="success" disabled>Confirmar reserva</button>
        </div>
        <div id="bookingStatus"></div>`;

    $("holdBtn").addEventListener("click", (e) => withSpinner(e.currentTarget, "Reservando...", createHold));
    $("confirmBtn").addEventListener("click", (e) => withSpinner(e.currentTarget, "Confirmando...", confirmBooking));
}

function resetBookingPanel() {
    $("bookingPanel").innerHTML =
        '<p class="empty">Selecione um horário disponível para iniciar a reserva.</p>';
}

async function createHold() {
    const slot = state.selectedSlot;
    const customerId = $("customerId").value.trim();
    if (!customerId) return toast("Informe o Customer ID.", "err");

    const idempotencyKey = uuid();
    try {
        const result = await api(`api/slots/${slot.slotId}/hold`, {
            method: "POST",
            headers: { "Idempotency-Key": idempotencyKey },
            body: JSON.stringify({ customerId, idempotencyKey }),
        });
        state.currentHoldId = result.holdId;
        const expires = new Date(result.expiresAt);
        $("bookingStatus").innerHTML =
            `<span class="status-badge status-ok">Hold criado</span>
             <p class="muted">Hold ${result.holdId}<br>Expira em ${fmtDate(expires)} ${fmtTime(expires)}</p>`;
        $("confirmBtn").disabled = false;
        toast("Hold criado com sucesso.", "ok");
    } catch (e) {
        state.currentHoldId = null;
        $("confirmBtn").disabled = true;
        $("bookingStatus").innerHTML = `<span class="status-badge status-err">Falha no hold</span><p class="muted">${e.message}</p>`;
        toast("Falha ao criar hold: " + e.message, "err");
    }
}

async function confirmBooking() {
    if (!state.currentHoldId) return toast("Crie um hold primeiro.", "err");
    const customerId = $("customerId").value.trim();
    const idempotencyKey = uuid();
    try {
        const result = await api(`api/holds/${state.currentHoldId}/confirm`, {
            method: "POST",
            headers: { "Idempotency-Key": idempotencyKey },
            body: JSON.stringify({ customerId, idempotencyKey }),
        });
        $("bookingStatus").innerHTML =
            `<span class="status-badge status-ok">Reserva confirmada</span>
             <p class="muted">Booking ${result.bookingId}</p>`;
        $("confirmBtn").disabled = true;
        $("holdBtn").disabled = true;
        toast("Reserva confirmada!", "ok");
    } catch (e) {
        $("bookingStatus").innerHTML = `<span class="status-badge status-err">Falha na confirmação</span><p class="muted">${e.message}</p>`;
        toast("Falha ao confirmar: " + e.message, "err");
    }
}

// ---------------------------------------------------------------------------
// Formatting
// ---------------------------------------------------------------------------
function fmtTime(d) {
    return d.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
}
function fmtDate(d) {
    return d.toLocaleDateString("pt-BR");
}
function pad2(n) { return String(n).padStart(2, "0"); }
function toLocalDateValue(d) {
    return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
}
function toLocalTimeValue(d) {
    return `${pad2(d.getHours())}:${pad2(d.getMinutes())}`;
}

// ===========================================================================
// Admin / CRUD de horários
// ===========================================================================
const admin = { editingId: null, professionalsLoaded: false };

function fillOptions(sel, items, valueOf, labelOf, firstLabel) {
    const previous = sel.value;
    sel.innerHTML = "";
    if (firstLabel !== undefined) {
        const opt = document.createElement("option");
        opt.value = "";
        opt.textContent = firstLabel;
        sel.appendChild(opt);
    }
    items.forEach((it) => {
        const opt = document.createElement("option");
        opt.value = valueOf(it);
        opt.textContent = labelOf(it);
        sel.appendChild(opt);
    });
    if (previous) sel.value = previous;
}

async function loadAdminReferenceData() {
    try {
        const [pros, units] = await Promise.all([api("api/professionals/all"), api("api/units")]);
        fillOptions($("adminProfessional"), pros, (p) => p.professionalId,
            (p) => `${p.professionalName} — ${p.specialtyName}`);
        fillOptions($("filterProfessional"), pros, (p) => p.professionalId,
            (p) => p.professionalName, "Todos");
        fillOptions($("adminUnit"), units, (u) => u.id, (u) => u.name);
        fillOptions($("filterUnit"), units, (u) => u.id, (u) => u.name, "Todas");
        admin.professionalsLoaded = true;
    } catch (e) {
        toast("Falha ao carregar dados do admin: " + e.message, "err");
    }
}

async function saveSlot() {
    const professionalId = $("adminProfessional").value;
    const clinicUnitId = $("adminUnit").value;
    const date = $("adminDate").value;
    const time = $("adminTime").value;
    const durationMinutes = parseInt($("adminDuration").value, 10) || 30;
    const seatCode = $("adminSeat").value.trim() || null;

    if (!professionalId) return toast("Selecione um profissional.", "err");
    if (!clinicUnitId) return toast("Selecione uma unidade.", "err");
    if (!date || !time) return toast("Informe data e hora.", "err");

    // Interpret the entered date/time as local wall-clock, send as UTC ISO.
    const startsAt = new Date(`${date}T${time}`).toISOString();

    try {
        if (admin.editingId) {
            await api(`api/slots/${admin.editingId}`, {
                method: "PUT",
                body: JSON.stringify({ startsAt, durationMinutes, seatCode }),
            });
            toast("Horário atualizado.", "ok");
        } else {
            await api("api/slots", {
                method: "POST",
                body: JSON.stringify({ professionalId, clinicUnitId, startsAt, durationMinutes, seatCode }),
            });
            toast("Horário adicionado.", "ok");
        }
        exitEditMode();
        await loadAdminSlots();
    } catch (e) {
        toast("Falha ao salvar: " + e.message, "err");
    }
}

function enterEditMode(slot) {
    admin.editingId = slot.slotId;
    const start = new Date(slot.startsAt);
    const durationMin = Math.max(5, Math.round((new Date(slot.endsAt) - start) / 60000));
    $("adminProfessional").value = slot.professionalId;
    $("adminUnit").value = slot.clinicUnitId;
    $("adminDate").value = toLocalDateValue(start);
    $("adminTime").value = toLocalTimeValue(start);
    $("adminDuration").value = durationMin;
    $("adminSeat").value = slot.seatCode || "";
    $("adminProfessional").disabled = true;
    $("adminUnit").disabled = true;
    $("slotFormTitle").textContent = "✏️ Editar horário";
    $("slotSaveBtn").textContent = "Salvar alterações";
    $("slotCancelEditBtn").classList.remove("hidden");
    window.scrollTo({ top: 0, behavior: "smooth" });
}

function exitEditMode() {
    admin.editingId = null;
    $("adminProfessional").disabled = false;
    $("adminUnit").disabled = false;
    $("adminSeat").value = "";
    $("slotFormTitle").textContent = "➕ Adicionar horário";
    $("slotSaveBtn").textContent = "Adicionar";
    $("slotCancelEditBtn").classList.add("hidden");
}

async function loadAdminSlots() {
    const params = new URLSearchParams();
    if ($("filterProfessional").value) params.set("professionalId", $("filterProfessional").value);
    if ($("filterUnit").value) params.set("unitId", $("filterUnit").value);
    if ($("filterDate").value) params.set("date", $("filterDate").value);

    const container = $("adminSlotsList");
    container.innerHTML = '<p class="empty"><span class="spinner"></span>Carregando...</p>';
    try {
        const qs = params.toString();
        const slots = await api("api/slots" + (qs ? "?" + qs : ""));
        renderAdminSlots(slots);
    } catch (e) {
        container.innerHTML = `<p class="empty">Erro: ${e.message}</p>`;
        toast("Falha ao carregar horários: " + e.message, "err");
    }
}

const STATUS_LABEL = { Available: "Disponível", Held: "Em hold", Booked: "Reservado" };

function renderAdminSlots(slots) {
    const container = $("adminSlotsList");
    container.innerHTML = "";
    if (!slots.length) {
        container.innerHTML = '<p class="empty">Nenhum horário para os filtros selecionados.</p>';
        return;
    }
    slots.forEach((slot) => {
        const start = new Date(slot.startsAt);
        const end = new Date(slot.endsAt);
        const statusClass = "status-" + slot.status.toLowerCase();
        const row = document.createElement("div");
        row.className = "row" + (slot.slotId === admin.editingId ? " editing" : "");
        row.style.cursor = "default";
        row.innerHTML = `
            <div>
                <div class="title">${fmtDate(start)} ${fmtTime(start)}–${fmtTime(end)}</div>
                <div class="meta">${slot.professionalName} &middot; ${slot.clinicUnitName}${slot.seatCode ? " &middot; " + slot.seatCode : ""}</div>
            </div>
            <div class="row-actions">
                <span class="status ${statusClass}">${STATUS_LABEL[slot.status] || slot.status}</span>
                <button class="btn-edit">Editar</button>
                <button class="btn-delete">Excluir</button>
            </div>`;
        row.querySelector(".btn-edit").addEventListener("click", () => enterEditMode(slot));
        row.querySelector(".btn-delete").addEventListener("click", () => deleteSlot(slot));
        container.appendChild(row);
    });
}

async function deleteSlot(slot) {
    const when = `${fmtDate(new Date(slot.startsAt))} ${fmtTime(new Date(slot.startsAt))}`;
    if (!confirm(`Excluir o horário de ${slot.professionalName} em ${when}?`)) return;
    try {
        await api(`api/slots/${slot.slotId}`, { method: "DELETE" });
        toast("Horário excluído.", "ok");
        if (admin.editingId === slot.slotId) exitEditMode();
        await loadAdminSlots();
    } catch (e) {
        toast("Falha ao excluir: " + e.message, "err");
    }
}

function switchTab(which) {
    const booking = which === "booking";
    $("tabBooking").classList.toggle("active", booking);
    $("tabAdmin").classList.toggle("active", !booking);
    $("viewBooking").classList.toggle("hidden", !booking);
    $("viewAdmin").classList.toggle("hidden", booking);
    if (!booking && !admin.professionalsLoaded) {
        loadAdminReferenceData();
    }
}

// ---------------------------------------------------------------------------
// Bootstrap
// ---------------------------------------------------------------------------
function init() {
    $("baseUrl").value = window.location.origin + "/";
    $("customerId").value = uuid();
    const today = new Date().toISOString().slice(0, 10);
    $("date").value = today;
    $("adminDate").value = today;

    $("newCustomerBtn").addEventListener("click", () => {
        $("customerId").value = uuid();
        toast("Novo Customer ID gerado.", "ok");
    });

    $("searchProfessionalsBtn").addEventListener("click", (e) =>
        withSpinner(e.currentTarget, "Buscando...", searchProfessionals).catch((err) =>
            toast("Falha na busca: " + err.message, "err")
        )
    );

    // Tabs
    $("tabBooking").addEventListener("click", () => switchTab("booking"));
    $("tabAdmin").addEventListener("click", () => switchTab("admin"));

    // Admin actions
    $("slotSaveBtn").addEventListener("click", (e) => withSpinner(e.currentTarget, "Salvando...", saveSlot));
    $("slotCancelEditBtn").addEventListener("click", exitEditMode);
    $("loadSlotsBtn").addEventListener("click", (e) => withSpinner(e.currentTarget, "Carregando...", loadAdminSlots));

    loadSpecialties();
    loadUnits();
}

document.addEventListener("DOMContentLoaded", init);
