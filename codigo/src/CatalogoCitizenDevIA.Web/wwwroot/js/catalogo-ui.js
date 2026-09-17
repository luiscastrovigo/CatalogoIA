// Interacciones de los listados de Catalogo y de Plataformas.
//
// (1) Confirmacion de borrado. El mensaje viaja en el atributo data-mensaje del
//     formulario y se lee desde aqui. Antes se armaba con
//     onsubmit="confirm('... nombre ...')", interpolando texto libre escrito por
//     cualquier usuario dentro de una cadena de JavaScript: la codificacion HTML
//     de Razor protege el atributo, pero no el contexto JS, asi que un apostrofo
//     en el nombre cerraba la cadena y permitia ejecutar codigo arbitrario en la
//     sesion del Administrador (XSS almacenado con escalada de privilegios,
//     hallazgo H-01 de la auditoria de seguridad).
//
// (2) Selects que envian su formulario al cambiar. Antes usaban
//     onchange="this.form.submit()"; los manejadores de evento inline tambien
//     quedan bloqueados por la CSP cuando se retira 'unsafe-inline' de
//     script-src (hallazgo H-12), asi que se enganchan aqui.
(function () {
    'use strict';

    var formularios = document.querySelectorAll('form.js-confirmar-eliminar');
    for (var i = 0; i < formularios.length; i++) {
        formularios[i].addEventListener('submit', function (e) {
            var mensaje = this.getAttribute('data-mensaje');
            if (mensaje && !window.confirm(mensaje)) { e.preventDefault(); }
        });
    }

    var autoenvio = document.querySelectorAll('.js-auto-submit');
    for (var j = 0; j < autoenvio.length; j++) {
        autoenvio[j].addEventListener('change', function () {
            if (this.form) { this.form.submit(); }
        });
    }
})();
