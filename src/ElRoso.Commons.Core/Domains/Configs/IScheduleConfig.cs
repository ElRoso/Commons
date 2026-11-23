// <copyright file="IScheduleConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Domains.Configs
{
    /// <summary>
    /// EN: The schedule itself, without the job type. Exists so <c>CronJobService</c> — which is not
    ///     generic — can receive the configuration registered by <c>AddCronJob&lt;T&gt;</c>. Before
    ///     this split the job read its cron expression from its constructor and never looked at the
    ///     registered config, so the expression passed to <c>AddCronJob</c> did nothing at all, in
    ///     silence.
    /// ES: El schedule en sí, sin el tipo del job. Existe para que <c>CronJobService</c> — que no es
    ///     genérico — pueda recibir la configuración que registra <c>AddCronJob&lt;T&gt;</c>. Antes
    ///     de esta separación el job leía su expresión cron del constructor y nunca miraba la config
    ///     registrada, así que la expresión pasada a <c>AddCronJob</c> no hacía nada, en silencio.
    /// </summary>
    public interface IScheduleConfig
    {
        string CronExpression { get; set; }

        TimeZoneInfo TimeZoneInfo { get; set; }
    }

    /// <summary>
    /// EN: Schedule bound to a job type, so each job resolves its own from DI.
    /// ES: Schedule atado a un tipo de job, para que cada uno resuelva el suyo desde DI.
    /// </summary>
    /// <typeparam name="T">The job this schedule belongs to. / El job al que pertenece.</typeparam>
    public interface IScheduleConfig<T> : IScheduleConfig
    {
    }
}
