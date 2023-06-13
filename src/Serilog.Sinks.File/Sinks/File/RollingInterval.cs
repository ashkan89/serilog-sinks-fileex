﻿namespace Serilog.Sinks.File;

/// <summary>
/// Specifies the frequency at which the log file should roll.
/// </summary>
public enum RollingInterval
{
    /// <summary>
    /// The log file will never roll; no time period information will be appended to the log file name.
    /// </summary>
    Infinite,

    /// <summary>
    /// Roll every year. FileNames will have a four-digit year appended in the pattern <code>yyyy</code>.
    /// </summary>
    Year,

    /// <summary>
    /// Roll every calendar month. FileNames will have <code>yyyyMM</code> appended.
    /// </summary>
    Month,

    /// <summary>
    /// Roll every day. FileNames will have <code>yyyyMMdd</code> appended.
    /// </summary>
    Day,

    /// <summary>
    /// Roll every hour. FileNames will have <code>yyyyMMddHH</code> appended.
    /// </summary>
    Hour,

    /// <summary>
    /// Roll every minute. FileNames will have <code>yyyyMMddHHmm</code> appended.
    /// </summary>
    Minute
}