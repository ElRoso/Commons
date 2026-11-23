// <copyright file="ElRosoControllerBase.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Presentation.WebAPP
{
    using ElRoso.Domains.Session;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    public abstract class ElRosoControllerBase : ControllerBase
    {
        protected ElRosoControllerBase(ILogger logger)
        {
            this.Logger = logger;
        }

        /// <summary>
        /// Gets the logger every controller already hands to this base class. It used to be a
        /// private field nobody could reach, so each derived controller injected its own logger on
        /// top of the one it was already passing up.
        /// ES: El logger que todo controller ya le pasa a esta clase base. Antes era un campo
        /// privado al que nadie podía llegar, así que cada controller derivado inyectaba su propio
        /// logger encima del que ya estaba pasando para arriba.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets the authenticated user, or <c>null</c>.
        /// 🔴 <c>null</c> also means <c>UseCurrentUserMiddleware()</c> is missing or misplaced in
        /// the pipeline — it must run after authentication and before the endpoints are mapped.
        /// ES: El usuario autenticado, o <c>null</c>.
        /// 🔴 <c>null</c> también significa que falta <c>UseCurrentUserMiddleware()</c> o que está
        /// mal ubicado en el pipeline — tiene que correr después de la autenticación y antes de
        /// mapear los endpoints.
        /// </summary>
        protected CurrentUser? CurrentUser => this.HttpContext?.Items["currentUser"] as CurrentUser;
    }
}
