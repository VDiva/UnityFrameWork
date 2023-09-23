using UnityEngine;
using NUnit.Framework;
using FlexFramework.Excel;
using System.IO;
using System.Text;
using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Linq.Expressions;
using System.Collections.Generic;

namespace FlexFramework.Tests
{
    public class CSVTests
    {
        [Test]
        public void ConverterTest()
        {
            Assert.AreEqual(1, ValueConverter.Convert<int>("1"));
            Assert.IsInstanceOf<Color>(ValueConverter.Convert(typeof(Color), "(1,1,1)"));
            Assert.Throws<System.FormatException>(() => ValueConverter.Convert<bool>("1"));
            Assert.IsInstanceOf<int[]>(ValueConverter.Convert<int[]>("1#2#3"));
            ValueConverter.Register(i => Int32.Parse(i) * 2);
            Assert.AreEqual(4, ValueConverter.Convert<int>("2"));
            ValueConverter.Reset();
        }

        [Test]
        public void DocumentTest()
        {
            var doc = Document.Load("a,b,c\n1,2,3");
            Assert.AreEqual(2, doc.Count);
            Assert.AreEqual(3, doc[0].Count);
            Assert.AreEqual("B2", doc[1][1].Address.ToString());
            Assert.AreEqual("a", doc[0][0].Value);
        }

        [Test]
        public void LineEndingTest()
        {
            var doc = Document.Load("a,b,c\n1,2,3\r4,5,6\r\n7,8,9");
            Assert.AreEqual(4, doc.Count);
        }

        [Test]
        public void SelectorTest()
        {
            var doc = Document.Load("a,b,c\n1,2,3\n4,5,6\n7,8,9");
            Assert.AreEqual("a", doc.Select("A1").Value);
            Assert.AreSame(doc[1][1], doc["B2"]);
            Assert.True(doc.SelectRange("A1:A3").Contains(doc["A2"]));
        }

        [Test]
        public void EmptyCellTest()
        {
            var doc = Document.Load("a,,\n,,\n,5,6");
            Assert.AreEqual(3, doc.Count);
            Assert.AreEqual(3, doc[0].Count);
            Assert.AreEqual(3, doc[1].Count);
            Assert.IsEmpty(doc[0][1].Text);
        }

        [Test]
        public void EmptyLineTest()
        {
            var doc = Document.Load("\n1,2,3\n\n");
            Assert.AreEqual(4, doc.Count);
            Assert.AreEqual(1, doc[0].Count);
            Assert.AreEqual(3, doc[1].Count);
            Assert.AreEqual(1, doc[3].Count);
        }

        [Test]
        public void DelimiterTest()
        {
            Document.Delimiter = ';';
            var doc = Document.Load("a;b;c\n1,2;3,4;5,6");
            Assert.AreEqual(3, doc[0].Count);
            Assert.AreEqual(3, doc[1].Count);
            Assert.AreEqual("1,2", doc[1][0].Value);
            Document.Reset();
        }

        [Test]
        public void EncloseTest()
        {
            var doc = Document.Load("a,b,c\n\"1,2,3\",4,5\n\"\"\"hello\"\"\"");
            Assert.AreEqual("1,2,3", doc[1][0].Value);
            Assert.AreEqual("\"hello\"", doc[2][0].Value);
        }

        [Test]
        public void BufferTest()
        {
            Document doc = null;
            var bin = "YSxiLGMNCjEsMiwzDQoiLCIsIiwiLCIsIg0KXG4sXG4sXG4NCiIiIiwiIiIsIiIiIiwiIiIiIiINCiIiIiIsLA0KImhlbGxvLHdvcmxkIiwsDQoiIiJoZWxsbyIiIiwsDQoiIiJoZWxsbyx3b3JsZCIiIiwsDQoiIiIsIiwsDQoiLCIiIiwsDQoiIiIiIiIiIiws";
            var buffer = Convert.FromBase64String(bin);
            Assert.DoesNotThrow(() => doc = Document.Load(buffer));
            Assert.Greater(doc.Count, 0);
        }

        [Test]
        public void MappingTest()
        {
            var doc = Document.Load("name,member,age,phone\nadam,true,22,933-311-5784");
            var mapper = new Mapper<User>().Map("name", 1).Map("member", "B").Map("age", 3).Map("phone", "D");
            User user = null;
            Assert.DoesNotThrow(() => user = doc[1].Convert(mapper));
            Assert.AreEqual("adam", user.name);
            Assert.True(user.member);
            Assert.AreEqual(22, user.age);
            Assert.AreEqual("933-311-5784", user.phone);
            Assert.DoesNotThrow(() => user = (User)doc[1].Convert(typeof(User), "name:1, member:B, age:C, phone:4"));
            Assert.AreEqual(22, user.age);
            Assert.DoesNotThrow(() => user = doc[1].Convert<User>(doc[0]));
            Assert.AreEqual("933-311-5784", user.phone);
            mapper.Remove("age").Remove("member").Remove("phone");
            Assert.DoesNotThrow(() => user = doc[1].Convert(mapper));
            Assert.AreEqual("adam", user.name);
            Assert.False(user.member);
            Assert.AreEqual(0, user.age);
            Assert.Null(user.phone);
        }

        [Test]
        public void TableMappingTest()
        {
            var doc = Document.Load("name,member,age,phone\nadam,true,22,927-553-6129\nbrian,false,18,354-459-2227\ncan,false,30,023-291-5783");
            var mapper = new TableMapper<User>().Map("name:1, member:2, age:3, phone:4").Exclude(1);
            User[] users = null;
            Assert.DoesNotThrow(() => users = doc.Convert(mapper).ToArray());
            Assert.AreEqual(3, users.Length);
            Assert.AreEqual(18, users[1].age);
            Assert.DoesNotThrow(() => users = doc.Convert<User>(1).ToArray());
            Assert.AreEqual("can", users[2].name);
        }

        [Test, Ignore("")]
        public void FormatterServicesTest()
        {
            for (int i = 0; i < 10000; i++)
            {
                FormatterServices.GetUninitializedObject(typeof(User));
            }
        }

        [Test, Ignore("")]
        public void ActivatorTest()
        {
            for (int i = 0; i < 10000; i++)
            {
                Activator.CreateInstance(typeof(User));
            }
        }

        [Test, Ignore("")]
        public void LambdaTest()
        {
            var func = Expression.Lambda<Func<User>>(Expression.New(typeof(User))).Compile();
            for (int i = 0; i < 10000; i++)
            {
                func();
            }
        }

        [Test]
        public void ExpandTest()
        {
            var table = Document.Load("1,2,3\n4,5,6\n7,8,9,10");
            var expanded = table.Expand();
            Assert.AreEqual(3, expanded.Count);
            Assert.AreEqual(4, expanded[0].Count);
            Assert.AreEqual(4, expanded[1].Count);
            Assert.AreEqual(4, expanded[2].Count);
            Assert.NotNull(expanded[0][3]);
            Assert.Null(expanded[0][3].Value);
            Assert.AreEqual("5", expanded.Select("B2").Value);
        }

        [Test]
        public void RotateTest()
        {
            var table = Document.Load("1,2,3\n4,5,6\n7,8,9,10");
            var rotated = table.Rotate();
            Assert.AreEqual(4, rotated.Count);
            Assert.AreEqual("7", rotated[0][0].Value);
            Assert.AreEqual("10", rotated.Select("A4").Value);
            Assert.NotNull(rotated[3][1]);
            Assert.Null(rotated[3][1].Value);
        }

        [Test]
        public void CollapseTest()
        {
            var table = Document.Load("\n1,,2,,\n,\n,3,\n,\n");
            var collapsed = table.Collapse();
            Assert.AreEqual(2, collapsed.Count);
            Assert.AreEqual(3, collapsed[0].Count);
            Assert.AreEqual(1, collapsed[1].Count);
        }

        [Test]
        public void FallbackTest()
        {
            var doc = Document.Load("name,member,age,phone\nadam,no,NaN,---");
            var mapper = new Mapper<User>().Map("name", 1).Map("member", 2).Map("age", 3).Map("phone", 4);
            User user = null;
            Assert.Throws<FormatException>(() => user = doc[1].Convert(mapper));
            mapper.SafeMode = true;
            Assert.DoesNotThrow(() => user = doc[1].Convert(mapper));
            Assert.False(user.member);
            Assert.AreEqual("adam", user.name);
            Assert.AreEqual(0, user.age);
            Assert.AreEqual("---", user.phone);
            mapper.Remove("member").Remove("age");
            Assert.Throws<ArgumentException>(() => mapper.Map("member", 2, 0));
            Assert.DoesNotThrow(() => mapper.Map("member", 2, true).Map("age", 3, 20));
            Assert.DoesNotThrow(() => user = doc[1].Convert(mapper));
            Assert.True(user.member);
            Assert.AreEqual(20, user.age);
        }

        [TestCase(10, 10), TestCase(100, 100), TestCase(1000, 100)]
        public void LoadPerformaceTest(int rows, int columns)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    sb.AppendFormat("{0}{1}", j, j < columns - 1 ? "," : string.Empty);
                }
                sb.AppendLine();
            }
            var plain = sb.ToString();
            Document.Load(plain);
        }

        [TestCase(10), TestCase(100), TestCase(1000), TestCase(5000)]
        public void ConvertPerformanceTest(int rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("name,member,age,phone");
            for (int i = 0; i < rows; i++)
            {
                sb.AppendFormat("{0},{1},{2},{3}", i, true, i, i);
                if (i < rows - 1)
                    sb.AppendLine();
            }
            var plain = sb.ToString();
            var doc = Document.Load(plain);
            var users = doc.Convert<User>(1).ToArray();
            Assert.AreEqual(rows, users.Length);
        }

        [Test]
        public void AttributeTest()
        {
            var doc = Document.Load("name,member,age,phone\n\nadam,false,NaN,---");
            Anotheruser[] users = null;
            Assert.DoesNotThrow(() => users = doc.Convert<Anotheruser>().ToArray());
            Assert.AreEqual(1, users.Length);
            Assert.AreEqual("adam", users[0].name);
            Assert.False(users[0].member);
            Assert.AreEqual(18, users[0].age);
            Assert.AreEqual("---", users[0].phone);
        }
    }
}

