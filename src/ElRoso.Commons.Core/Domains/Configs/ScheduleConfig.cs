// <copyright file="ScheduleConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Domains.Configs
{
    using System;

    public class ScheduleConfig<T> : IScheduleConfig<T>
    {
        public string CronExpression { get; set; } = string.Empty;

        public TimeZoneInfo TimeZoneInfo { get; set; } = TimeZoneInfo.Utc;
    }
}
