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

// ---------------------------------------------------------------------------
// Bootstrap
// ---------------------------------------------------------------------------
function init() {
    $("baseUrl").value = window.location.origin + "/";
    $("customerId").value = uuid();
    $("date").value = new Date().toISOString().slice(0, 10);

    $("newCustomerBtn").addEventListener("click", () => {
        $("customerId").value = uuid();
        toast("Novo Customer ID gerado.", "ok");
    });

    $("searchProfessionalsBtn").addEventListener("click", (e) =>
        withSpinner(e.currentTarget, "Buscando...", searchProfessionals).catch((err) =>
            toast("Falha na busca: " + err.message, "err")
        )
    );

    loadSpecialties();
    loadUnits();
}

document.addEventListener("DOMContentLoaded", init);
