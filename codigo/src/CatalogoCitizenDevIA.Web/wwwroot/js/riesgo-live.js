// Calculo de riesgo en vivo + proteccion contra doble envio.
//
// Este codigo vivia como bloque <script> inline dentro de Views/Solucion/Create.cshtml
// y Views/Solucion/Editar.cshtml. Se movio a un archivo estatico como parte de la
// auditoria de seguridad (hallazgo H-12): con el JS fuera del HTML, la politica de
// seguridad de contenido (CSP) puede declarar script-src 'self' SIN 'unsafe-inline',
// de modo que el navegador se niegue a ejecutar cualquier script incrustado en la
// pagina — la ultima linea de defensa frente a una inyeccion.
//
// La URL del endpoint de calculo llega por el atributo data-url-calcular del
// formulario (la escribe Razor con Url.Action), en vez de interpolarse en el JS.
// El comportamiento visible es identico al que habia antes.
(function () {
    'use strict';

    var form = document.querySelector('form[data-url-calcular]');
    if (!form) { return; }

    var urlCalcular = form.getAttribute('data-url-calcular');
    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    var token = tokenInput ? tokenInput.value : '';

    var badgeNivel = document.getElementById('badge-nivel');
    var badgeRevision = document.getElementById('badge-revision');
    var nivelClase = { 'Nivel 1': 'tag-nivel-1', 'Nivel 2': 'tag-nivel-2', 'Nivel 3': 'tag-nivel-3' };

    // Doble-registro por doble clic: solo aplica a la pantalla de registro, que es
    // la unica que trae el boton #btn-registrar. Se engancha al evento submit del
    // formulario (no al click del boton) para no interferir con la validacion
    // HTML5 de campos obligatorios.
    var btnRegistrar = document.getElementById('btn-registrar');
    var msgProcesando = document.getElementById('msg-procesando');
    if (btnRegistrar) {
        form.addEventListener('submit', function () {
            btnRegistrar.disabled = true;
            btnRegistrar.textContent = 'Procesando\u2026';
            if (msgProcesando) { msgProcesando.style.display = 'inline'; }
        });
    }

    function marcado(id) { var el = document.getElementById(id); return el ? el.checked : false; }
    function valor(id) { var el = document.getElementById(id); return el ? el.value : ''; }

    function recalcular() {
        if (!badgeNivel || !urlCalcular) { return; }

        var payload = {
            esProcesoCritico: marcado('EsProcesoCritico'),
            alcanceUso: valor('AlcanceUso'),
            requiereConexionCentral: marcado('RequiereConexionCentral'),
            usaPii: marcado('UsaPii'),
            requierePublicacionInternet: marcado('RequierePublicacionInternet'),
            requiereInfraDedicada: valor('RequiereInfraDedicada')
        };

        fetch(urlCalcular, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify(payload)
        }).then(function (resp) {
            return resp.ok ? resp.json() : null;
        }).then(function (data) {
            if (!data) { return; }
            badgeNivel.textContent = data.nivel;
            badgeNivel.className = 'tag ' + (nivelClase[data.nivel] || 'tag-neutral');
            if (badgeRevision) { badgeRevision.textContent = data.proximaRevision; }
        }).catch(function () {
            // Silencioso a proposito: el nivel definitivo siempre lo recalcula el
            // servidor al grabar (08-reglas-negocio-calculo-riesgo.md).
        });
    }

    var entradas = document.querySelectorAll('.riesgo-input');
    for (var i = 0; i < entradas.length; i++) {
        entradas[i].addEventListener('change', recalcular);
    }
    recalcular();
})();
