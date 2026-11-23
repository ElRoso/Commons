// <copyright file="ScheduledServiceExtension.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services.Extensions
{
    using Cronos;
    using ElRoso.Domains.Configs;
    using Microsoft.Extensions.DependencyInjection;
    using System;

    public static class ScheduledServiceExtension
    {
        /// <summary>
        /// EN: Registers a cron job and its schedule.
        ///     ⚠️ For the expression given here to actually govern the job, the job must take
        ///     <see cref="IScheduleConfig{T}"/> in its constructor and pass it to the base class.
        ///     A job built with the raw-string constructor ignores this configuration entirely.
        /// ES: Registra un cron job y su schedule.
        ///     ⚠️ Para que la expresión que se pasa acá realmente gobierne al job, el job tiene que
        ///     recibir <see cref="IScheduleConfig{T}"/> por constructor y pasárselo a la clase base.
        ///     Un job construido con el constructor de string ignora esta configuración por completo.
        /// </summary>
        /// <returns>The service collection, for chaining.</returns>
        public static IServiceCollection AddCronJob<T>(this IServiceCollection services, Action<IScheduleConfig<T>> options)
            where T : CronJobService
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options), @"Please provide Schedule Configurations.");
            }

            var config = new ScheduleConfig<T>();
            options.Invoke(config);
            if (string.IsNullOrWhiteSpace(config.CronExpression))
            {
                throw new ArgumentNullException(nameof(ScheduleConfig<T>.CronExpression), @"Empty Cron Expression is not allowed.");
            }

            // EN: Parse at registration so a malformed expression fails the startup, next to the
            //     line that wrote it. It used to be accepted here and only blow up when the job
            //     built its own — or never, if the job read its schedule from the constructor.
            //     Wrapped as ArgumentException to match the other guards of this method; the
            //     original CronFormatException travels as the inner exception with its details.
            // ES: Parsear al registrar para que una expresión mal escrita rompa el arranque, al lado
            //     de la línea que la escribió. Antes se aceptaba acá y explotaba recién cuando el
            //     job armaba la suya — o nunca, si el job leía su schedule del constructor.
            //     Se envuelve en ArgumentException para ser consistente con los otros guards del
            //     método; la CronFormatException original viaja como inner, con su detalle.
            try
            {
                CronExpression.Parse(config.CronExpression);
            }
            catch (CronFormatException ex)
            {
                throw new ArgumentException(
                    $"'{config.CronExpression}' is not a valid cron expression. / No es una expresión cron válida.",
                    nameof(ScheduleConfig<T>.CronExpression),
                    ex);
            }

            services.AddSingleton<IScheduleConfig<T>>(config);
            services.AddHostedService<T>();
            return services;
        }
    }
}
