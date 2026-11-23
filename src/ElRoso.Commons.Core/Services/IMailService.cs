// <copyright file="IMailService.cs" company="El Roso">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using ElRoso.Domains.Requests;

    public interface IMailService : IElRosoServiceBase
    {
        /// <summary>
        /// EN: Sends through SMTP. The token is optional so existing callers keep compiling, but a
        ///     send with no way to cancel it is a request that can hang for as long as the SDK's own
        ///     timeout — pass one.
        /// ES: Envía por SMTP. El token es opcional para que los llamadores existentes sigan
        ///     compilando, pero un envío sin forma de cancelarlo es un request que puede colgarse
        ///     tanto como el timeout del propio SDK — conviene pasarlo.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task SendSMTPAsync(EmailRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// EN: Sends through Microsoft Graph. Throws <see cref="InvalidOperationException"/> when
        ///     <c>AzureMailService:*</c> is not configured.
        /// ES: Envía por Microsoft Graph. Lanza <see cref="InvalidOperationException"/> si no está
        ///     configurado <c>AzureMailService:*</c>.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task SendGraphAsync(EmailRequest request, CancellationToken cancellationToken = default);
    }
}
