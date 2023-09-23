using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FlexFramework.Excel
{
    public interface IConverter
    {
        object Convert(string input);

        Type Type { get; }
    }
}