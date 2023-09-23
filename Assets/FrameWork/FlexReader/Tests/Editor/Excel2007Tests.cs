using NUnit.Framework;
using FlexFramework.Excel;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FlexFramework.Tests
{
    public class Excel2007Tests
    {
        public readonly string FilePath = "Assets/FlexReader/Tests/Editor/test.xlsx.bytes";

        [Test]
        public void LoadTest()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FilePath);
            WorkBook book = null;
            Assert.DoesNotThrow(() => book = new WorkBook(asset.bytes));
            Assert.AreEqual(4, book.Count);
            Assert.AreEqual("GeneralTest", book[0].Name);
            Assert.AreEqual("TableMappingTest", book[1].Name);
            Assert.AreEqual("CellTypesTest", book[2].Name);
            Assert.AreEqual("FormatTest", book[3].Name);
        }

        [Test]
        public void GeneralTest()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FilePath);
            WorkBook book = null;
            Assert.DoesNotThrow(() => book = new WorkBook(asset.bytes));
            var doc = book["GeneralTest"];
            Assert.AreEqual(7, doc.Count);
            Assert.AreEqual(4, doc[0].Count);
            Assert.True(doc[0][0].IsString);
            Assert.True(doc[0][3].IsInteger);
            Assert.AreEqual(11, doc[0][3].Integer);
            Assert.True(book.Select("GeneralTest!A4").IsBoolean);
            Assert.True(book.Select("GeneralTest!D3").IsSpan);
            Assert.AreEqual(12, book.SelectRange("GeneralTest!A1:D3").Count());
            Assert.True(doc.Select("A7").IsInteger);
            int value = 0;
            Assert.Throws<InvalidCastException>(() => value = doc.Select("B7").Integer);
            book.Merge();
            Assert.DoesNotThrow(() => value = doc.Select("B7").Integer);
            Assert.AreEqual(10, book["GeneralTest"]["B7"].Value);
            Assert.AreEqual(10, value);
        }

        [Test]
        public void TableMappingTest()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FilePath);
            WorkBook book = null;
            Assert.DoesNotThrow(() => book = new WorkBook(asset.bytes));
            var doc = book["TableMappingTest"];
            var mapper = new TableMapper<User>().Map("name:1, member:2, age:3, phone:4").Exclude(1);
            User[] users = null;
            Assert.DoesNotThrow(() => users = doc.Convert(mapper).ToArray());
            Assert.AreEqual(3, users.Length);
            Assert.AreEqual(18, users[1].age);
            Assert.DoesNotThrow(() => users = doc.Convert<User>(1).ToArray());
            Assert.AreEqual("can", users[2].name);
        }

        [Test]
        public void CellTypesTest()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FilePath);
            WorkBook book = null;
            Assert.DoesNotThrow(() => book = new WorkBook(asset.bytes));
            var doc = book["CellTypesTest"];
            Assert.True(doc["A2"].IsString);
            Assert.True(doc["B2"].IsInteger);
            Assert.True(doc["C2"].IsSingle);
            Assert.True(doc["D2"].IsBoolean);
            Assert.True(doc["E2"].IsInteger);
            Assert.True(doc["F2"].IsString);
            Assert.True(doc["G2"].IsInteger);
            Assert.True(doc["H2"].IsSingle);
            Assert.True(doc["I2"].IsBoolean);
            Assert.True(doc["J2"].IsInteger);
        }

        [Test]
        public void FormatTest()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FilePath);
            WorkBook book = null;
            Assert.DoesNotThrow(() => book = new WorkBook(asset.bytes));
            var doc = book["FormatTest"];
            Assert.AreEqual(5, doc.Count);
            Assert.AreEqual(4, doc[0].Count);
            Assert.AreEqual(6, doc[2].Count);
            Assert.AreEqual(5, doc[3].Count);
            Assert.AreEqual("bold", doc[0][0].Value);
            Assert.AreEqual("italic", doc[0][1].Value);
            Assert.IsNull(doc[0][2].Value);
            Assert.AreEqual("underline", doc[0][3].Value);
            Assert.IsNull(doc[1][0].Value);
            Assert.AreEqual("background", doc[2][5].Value);
            Assert.AreEqual("bold italic", doc[3][0].Value);
            Assert.AreEqual(123, doc[3][2].Value);
            Assert.AreEqual("color", doc[3][4].Value);
            Assert.AreEqual("123456", doc["F5"].Value);
            Assert.AreEqual("abcdef", doc["G5"].Value);
            Assert.AreEqual("都道府県", doc["B5"].Value);
        }
    }

}