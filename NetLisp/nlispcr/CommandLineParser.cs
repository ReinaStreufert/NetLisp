using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nlispcr
{
    class NlispcrCommandLineParser : CommandLineParser
    {
        private Switch sourceSwitch = new Switch('s', "source", SwitchArgumentType.String, new FileSwitchValidator());
        private Switch libpathSwitch = new Switch('l', "libpath", SwitchArgumentType.String, new DirSwitchValidator());
        private Switch resultSwitch = new Switch('r', "result", SwitchArgumentType.None);
        private Switch versionSwitch = new Switch('v', "version", SwitchArgumentType.None);
        private Switch helpSwitch = new Switch('?', "help", SwitchArgumentType.None);

        protected override Switch[] Switches => new[]
        {
            sourceSwitch, 
            libpathSwitch,
            resultSwitch,
            versionSwitch, 
            helpSwitch 
        };

        public string? Source
        {
            get
            {
                if (!sourceSwitch.IsSet)
                {
                    return null;
                }
                return (string)sourceSwitch.ArgumentValue;
            }
        }
        public string? LibpathDir
        {
            get
            {
                if (!libpathSwitch.IsSet)
                {
                    return null;
                }
                return (string)libpathSwitch.ArgumentValue;
            }
        }
        public bool Result
        {
            get => resultSwitch.IsSet;
        }
        public bool Version
        {
            get => versionSwitch.IsSet;
        }
        public bool Help
        {
            get => helpSwitch.IsSet;
        }
    }
    abstract class CommandLineParser
    {
        protected enum SwitchArgumentType
        {
            None,
            String,
            Integer,
            Float
        }
        protected class Switch
        {
            public char SwitchChar;
            public string SwitchFullName;
            public SwitchArgumentType ArgumentType;
            public SwitchValidator Validator;
            public bool IsSet = false;
            public object? ArgumentValue = null;
            public Switch(char switchChar, string switchFullName, SwitchArgumentType argumentType, SwitchValidator validator = null)
            {
                SwitchChar = switchChar;
                SwitchFullName = switchFullName;
                ArgumentType = argumentType;
                Validator = validator;
            }
        }
        protected abstract class SwitchValidator
        {
            public abstract void Validate(Switch cmdSwitch);
        }
        protected class FileSwitchValidator : SwitchValidator
        {
            public override void Validate(Switch cmdSwitch)
            {
                if (!File.Exists((string)cmdSwitch.ArgumentValue))
                {
                    Console.WriteLine("File path not found: " + (string)cmdSwitch.ArgumentValue);
                    Environment.Exit(-1);
                }
            }
        }
        protected class DirSwitchValidator : SwitchValidator
        {
            public override void Validate(Switch cmdSwitch)
            {
                if (!Directory.Exists((string)cmdSwitch.ArgumentValue))
                {
                    Console.WriteLine("Dir path not found: " + (string)cmdSwitch.ArgumentValue);
                    Environment.Exit(-1);
                }
            }
        }
        protected abstract Switch[] Switches { get; }
        public bool ParseSwitches()
        {
            string[] cmdArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
            bool expectSwitch = true;
            Switch currentSwitch = null;
            foreach (string arg in cmdArgs)
            {
                if (expectSwitch)
                {
                    currentSwitch = null;
                    if (arg.StartsWith("--"))
                    {
                        string fullName = arg.Substring(2);
                        foreach (Switch sw in Switches)
                        {
                            if (sw.SwitchFullName == fullName)
                            {
                                currentSwitch = sw;
                                break;
                            }
                        }
                    }
                    else if (arg.StartsWith("-") && arg.Length == 2)
                    {
                        char switchChar = arg[1];
                        foreach (Switch sw in Switches)
                        {
                            if (sw.SwitchChar == switchChar)
                            {
                                currentSwitch = sw;
                                break;
                            }
                        }
                    }
                    else
                    {
                        return false;
                    }
                    if (currentSwitch == null)
                    {
                        return false;
                    }
                    currentSwitch.IsSet = true;
                    if (currentSwitch.ArgumentType != SwitchArgumentType.None)
                    {
                        expectSwitch = false;
                    }
                } else
                {
                    if (currentSwitch.ArgumentType == SwitchArgumentType.Integer)
                    {
                        int val;
                        if (!int.TryParse(arg, out val))
                        {
                            return false;
                        }
                        currentSwitch.ArgumentValue = val;
                    } else if (currentSwitch.ArgumentType == SwitchArgumentType.Float)
                    {
                        float val;
                        if (!float.TryParse(arg, out val))
                        {
                            return false;
                        }
                        currentSwitch.ArgumentValue = val;
                    } else if (currentSwitch.ArgumentType == SwitchArgumentType.String)
                    {
                        currentSwitch.ArgumentValue = arg;
                    }
                    if (currentSwitch.Validator != null)
                    {
                        currentSwitch.Validator.Validate(currentSwitch);
                    }
                    expectSwitch = true;
                }
            }
            return true;
        }
    }
}
