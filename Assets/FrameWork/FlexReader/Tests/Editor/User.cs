#pragma warning disable 649
using System;
using FlexFramework.Excel;

namespace FlexFramework.Tests
{
    [Serializable]
    class User
    {
        public string name;
        public bool member;
        public int age;
        public string phone;
    }

    [Serializable, Table(1, 2)]
    class Anotheruser
    {
        [Column(1)] public string name;
        [Column("B", true)] public bool member;
        [Column(3, 18)] public int age;
        [Column("D")] public string phone;
    }
}