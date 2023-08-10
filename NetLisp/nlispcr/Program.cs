using nlispcr;

NlispcrCommandLineParser commandLineArgs = new NlispcrCommandLineParser();
if (!commandLineArgs.ParseSwitches())
{
    HelpDisplay.Start();
} else
{
    if (commandLineArgs.Help)
    {
        HelpDisplay.Start();
    } else if (commandLineArgs.Version)
    {
        VersionDisplay.Start();
    } else
    {
        SourceExecution.Start(commandLineArgs);
    }
}