﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace BinaryKits.Zpl.Protocol.Commands.UnitTest
{
    [TestClass]
    public class Code128BarCodeCommandTest
    {
        [TestMethod]
        public void ToZpl_Default1_Successful()
        {
            var command = new Code128BarCodeCommand();
            var zplCommand = command.ToZpl();
            Assert.AreEqual("^BCN,,Y,N,N", zplCommand);
        }

        [TestMethod]
        public void ToZpl_Default2_Successful()
        {
            var command = new Code128BarCodeCommand(Orientation.Rotated180);
            var zplCommand = command.ToZpl();
            Assert.AreEqual("^BCI,,Y,N,N", zplCommand);
        }

        [TestMethod]
        public void ToZpl_Default3_Successful()
        {
            var command = new Code128BarCodeCommand(barCodeHeight: 10);
            var zplCommand = command.ToZpl();
            Assert.AreEqual("^BCN,10,Y,N,N", zplCommand);
        }

        [TestMethod]
        public void ToZpl_Default4_Successful()
        {
            var command = new Code128BarCodeCommand(printInterpretationLine: false, printInterpretationLineAboveCode: false, uccCheckDigit: true);
            var zplCommand = command.ToZpl();
            Assert.AreEqual("^BCN,,N,N,Y", zplCommand);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Constructor_InvalidBarCodeHeight1_Exception()
        {
            new Code128BarCodeCommand(barCodeHeight: 0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Constructor_InvalidBarCodeHeight2_Exception()
        {
            new Code128BarCodeCommand(barCodeHeight: 33000);
        }

        [TestMethod]
        public void IsCommandParsable_ValidCommand_True()
        {
            var isParsable = Code128BarCodeCommand.CanParseCommand("^BCN,,N,N,Y");
            Assert.IsTrue(isParsable);
        }

        [TestMethod]
        public void IsCommandParsable_InvalidCommand_False()
        {
            var isParsable = Code128BarCodeCommand.CanParseCommand("^FT10,10");
            Assert.IsFalse(isParsable);
        }

        [TestMethod]
        public void ParseCommand_ValidCommand1_Successful()
        {
            var command = CommandBase.ParseCommand("^BCN,,N,N,Y");
            Assert.IsTrue(command is Code128BarCodeCommand);
            if (command is Code128BarCodeCommand code128Command)
            {
                Assert.AreEqual(Orientation.Normal, code128Command.Orientation);
                Assert.IsNull(code128Command.BarCodeHeight);
                Assert.IsFalse(code128Command.PrintInterpretationLine);
                Assert.IsFalse(code128Command.PrintInterpretationLineAboveCode);
                Assert.IsTrue(code128Command.UccCheckDigit);
            }
        }

        [TestMethod]
        public void ParseCommand_ValidCommand2_Successful()
        {
            var command = CommandBase.ParseCommand("^BCN,,Y,Y,Y");
            Assert.IsTrue(command is Code128BarCodeCommand);
            if (command is Code128BarCodeCommand code128Command)
            {
                Assert.AreEqual(Orientation.Normal, code128Command.Orientation);
                Assert.IsNull(code128Command.BarCodeHeight);
                Assert.IsTrue(code128Command.PrintInterpretationLine);
                Assert.IsTrue(code128Command.PrintInterpretationLineAboveCode);
                Assert.IsTrue(code128Command.UccCheckDigit);
            }
        }

        [TestMethod]
        public void ParseCommand_ValidCommand3_Successful()
        {
            var command = CommandBase.ParseCommand("^BCN,,N,N,N");
            Assert.IsTrue(command is Code128BarCodeCommand);
            if (command is Code128BarCodeCommand code128Command)
            {
                Assert.AreEqual(Orientation.Normal, code128Command.Orientation);
                Assert.IsNull(code128Command.BarCodeHeight);
                Assert.IsFalse(code128Command.PrintInterpretationLine);
                Assert.IsFalse(code128Command.PrintInterpretationLineAboveCode);
                Assert.IsFalse(code128Command.UccCheckDigit);
            }
        }

        [TestMethod]
        public void ParseCommand_ValidCommand4_Successful()
        {
            var command = CommandBase.ParseCommand("^BCR,20,N,N,N");
            Assert.IsTrue(command is Code128BarCodeCommand);
            if (command is Code128BarCodeCommand code128Command)
            {
                Assert.AreEqual(Orientation.Rotated90, code128Command.Orientation);
                Assert.AreEqual(20, code128Command.BarCodeHeight);
                Assert.IsFalse(code128Command.PrintInterpretationLine);
                Assert.IsFalse(code128Command.PrintInterpretationLineAboveCode);
                Assert.IsFalse(code128Command.UccCheckDigit);
            }
        }

        [TestMethod]
        public void ParseCommand_ValidCommand5_Successful()
        {
            var command = CommandBase.ParseCommand("^BCB,55");
            Assert.IsTrue(command is Code128BarCodeCommand);
            if (command is Code128BarCodeCommand code128Command)
            {
                Assert.AreEqual(Orientation.Rotated270, code128Command.Orientation);
                Assert.AreEqual(55, code128Command.BarCodeHeight);
                Assert.IsTrue(code128Command.PrintInterpretationLine);
                Assert.IsFalse(code128Command.PrintInterpretationLineAboveCode);
                Assert.IsFalse(code128Command.UccCheckDigit);
            }
        }

        [TestMethod]
        public void ParseCommand_ValidCommand6_Successful()
        {
            var command = CommandBase.ParseCommand("^BC");
            Assert.IsTrue(command is Code128BarCodeCommand);
            if (command is Code128BarCodeCommand code128Command)
            {
                Assert.AreEqual(Orientation.Normal, code128Command.Orientation);
                Assert.IsNull(code128Command.BarCodeHeight);
                Assert.IsTrue(code128Command.PrintInterpretationLine);
                Assert.IsFalse(code128Command.PrintInterpretationLineAboveCode);
                Assert.IsFalse(code128Command.UccCheckDigit);
            }
        }

    }
}
