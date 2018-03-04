﻿using System;
using System.Collections.Generic;
using System.Text;
using VtNetCore.VirtualTerminal;
using VtNetCore.XTermParser;
using Xunit;

namespace VtNetCoreUnitTests
{
    public class LibvtermStateEdit
    {
        private void Push(DataConsumer d, string s)
        {
            d.Push(Encoding.UTF8.GetBytes(s));
        }

        private bool IsCursor(VirtualTerminalController t, int row, int column)
        {
            return t.ViewPort.CursorPosition.Row == row && t.ViewPort.CursorPosition.Column == column;
        }

        //  "000000000111111111122222222223333333333444444444455555555556666666666777777777788
        //  "123456789012345678901234567890123456789012345678901234567890123456789012345678901"
        public static readonly string ExpectLine1Start =
            "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab ";

        public static readonly string ExpectInsertChar =
            "A CDdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyza ";
        public static readonly string ExpectInsertCharEdited =
            "ABCDdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyza ";
        public static readonly string ExpectInsertCharThreeMore =
            "AB   CDdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwx ";

        // !ICH
        // RESET
        //   erase 0..25,0..80
        //   ?cursor = 0,0
        // PUSH "ACD"
        // PUSH "\e[2D"
        //   ?cursor = 0,1
        // PUSH "\e[@"
        //   scrollrect 0..1,1..80 => +0,-1
        //   ?cursor = 0,1
        // PUSH "B"
        //   ?cursor = 0,2
        // PUSH "\e[3@"
        //   scrollrect 0..1,2..80 => +0,-3
        [Fact]
        public void InsertCharacter()
        {
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingDiagonalLower();

            Push(d, "".RIS());

            var s = t.GetScreenText();
            Assert.Equal("", s.Trim());
            Assert.Equal(0, t.ViewPort.CursorPosition.Row);
            Assert.Equal(0, t.ViewPort.CursorPosition.Column);

            t.TestPatternScrollingDiagonalLower();
            s = t.GetVisibleChars(0, 0, 81);

            Push(d, "ACD".CUB(2));
            Assert.Equal(0, t.ViewPort.CursorPosition.Row);
            Assert.Equal(1, t.ViewPort.CursorPosition.Column);

            Push(d, "".ICH());

            s = t.GetVisibleChars(0, 0, 81);
            Assert.Equal(ExpectInsertChar, s);

            Push(d, "B");

            s = t.GetVisibleChars(0, 0, 81);
            Assert.Equal(ExpectInsertCharEdited, s);
            Assert.Equal(0, t.ViewPort.CursorPosition.Row);
            Assert.Equal(2, t.ViewPort.CursorPosition.Column);

            Push(d, "".ICH(3));

            s = t.GetVisibleChars(0, 0, 81);
            Assert.Equal(ExpectInsertCharThreeMore, s);
        }

        //  "000000000111111111122222222223333333333444444444455555555556666666666777777777788
        //  "123456789012345678901234567890123456789012345678901234567890123456789012345678901"
        //  "                   |                             |
        //  "AB   CDdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwx " // Before
        //  "AB   CDdefghijklmno pqrstuvwxyzabcdefghijklmnopqrsuvwxyzabcdefghijklmnopqrstuvwx " // After
        //  "AB   CDdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwx 
        public static readonly string ExpectInsertCharacterWithDECSLRMAfter =
            "AB   CDdefghijklmno pqrstuvwxyzabcdefghijklmnopqrsuvwxyzabcdefghijklmnopqrstuvwx ";

        // !ICH with DECSLRM
        // PUSH "\e[?69h"
        // PUSH "\e[;50s"
        // PUSH "\e[20G\e[@"
        //   scrollrect 0..1,19..50 => +0,-1
        [Fact]
        public void InsertCharacterWithDECSLRM()
        {
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingDiagonalLower();

            // From InsertCharacter
            Push(d, "ACD".CUB(2).ICH().T("B").ICH(3));
            var s = t.GetVisibleChars(0, 0, 81);
            Assert.Equal(ExpectInsertCharThreeMore, s);

            Push(d, "".EnableLRMM().LRMM(1, 50).CHA(20));
            Assert.Equal(0, t.ViewPort.CursorPosition.Row);
            Assert.Equal(19, t.ViewPort.CursorPosition.Column);

            Push(d, "".ICH());

            s = t.GetVisibleChars(0, 0, 81);
            Assert.Equal(ExpectInsertCharacterWithDECSLRMAfter, s);
        }

        // !ICH outside DECSLRM
        // PUSH "\e[70G\e[@"
        //   # nothing happens
        [Fact]
        public void InsertCharacterOutsideDECSLRM()
        {
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingDiagonalLower();

            // From InsertCharacter
            Push(d, "ACD".CUB(2).ICH().T("B").ICH(3).EnableLRMM().LRMM(1, 50).CHA(20).ICH());
            var s = t.GetVisibleChars(0, 0, 81);
            Assert.Equal(ExpectInsertCharacterWithDECSLRMAfter, s);

            Push(d, "".CHA(70).ICH());

            Assert.Equal(0, t.ViewPort.CursorPosition.Row);
            Assert.Equal(69, t.ViewPort.CursorPosition.Column);

            s = t.GetVisibleChars(0, 0, 81);
            Assert.Equal(ExpectInsertCharacterWithDECSLRMAfter, s);
        }

        //  "000000000111111111122222222223333333333444444444455555555556666666666777777777788
        //  "123456789012345678901234567890123456789012345678901234567890123456789012345678901"
        //  "ABBCefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab " // Before
        //  "ABCefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab  " // DCH
        //  "Afghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab     " // DCH(3)
        public static readonly string ExpectDCH =
            "ABCefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab  ";
        public static readonly string ExpectDCH3 =
            "Afghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab     ";

        // !DCH
        // RESET
        //   erase 0..25,0..80
        //   ?cursor = 0,0
        // PUSH "ABBC"
        // PUSH "\e[3D"
        //   ?cursor = 0,1
        // PUSH "\e[P"
        //   scrollrect 0..1,1..80 => +0,+1
        //   ?cursor = 0,1
        // PUSH "\e[3P"
        //   scrollrect 0..1,1..80 => +0,+3
        //   ?cursor = 0,1
        [Fact]
        public void DeleteCharacter()
        {
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingDiagonalLower();

            Push(d, "ABBC".CUB(3));
            Assert.True(IsCursor(t, 0, 1));

            Push(d, "".DCH());
            var s = t.GetVisibleChars(0, 0, 81);
            Assert.True(IsCursor(t, 0, 1));
            Assert.Equal(ExpectDCH, s);

            Push(d, "".DCH(3));
            s = t.GetVisibleChars(0, 0, 81);
            Assert.True(IsCursor(t, 0, 1));
            Assert.Equal(ExpectDCH3, s);
        }

        //  "000000000111111111122222222223333333333444444444455555555556666666666777777777788"
        //  "123456789012345678901234567890123456789012345678901234567890123456789012345678901"
        //  "|                  *                             |                               " // Margins and position
        //  "Afghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab     " // Before
        //  "Afghijklmnopqrstuvwyzabcdefghijklmnopqrstuvwxyzab cdefghijklmnopqrstuvwxyzab     " // After
        public static readonly string ExpectDCHInDECSLRM =
            "Afghijklmnopqrstuvwyzabcdefghijklmnopqrstuvwxyzab cdefghijklmnopqrstuvwxyzab     ";

        // !DCH with DECSLRM
        // PUSH "\e[?69h"
        // PUSH "\e[;50s"
        // PUSH "\e[20G\e[P"
        //   scrollrect 0..1,19..50 => +0,+1
        [Fact]
        public void DeleteCharacterInDECSLRM()
        {
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingDiagonalLower();

            // From DeleteCharacter and DeleteCharacterInDECSLRM
            Push(d, "ABBC".CUB(3).DCH().DCH(3).EnableLRMM().LRMM(1,50).CHA(20).DCH());

            var s = t.GetVisibleChars(0, 0, 81);
            Assert.Equal(ExpectDCHInDECSLRM, s);
        }

        // !DCH outside DECSLRM
        // PUSH "\e[70G\e[P"
        //   # nothing happens
        [Fact]
        public void DeleteCharacterOutsideDECSLRM()
        {
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingDiagonalLower();

            // From DeleteCharacter and DeleteCharacterInDECSLRM
            Push(d, "ABBC".CUB(3).DCH().DCH(3).EnableLRMM().LRMM(1, 50).CHA(20).DCH().CHA(70).DCH());

            var s = t.GetVisibleChars(0, 0, 81);
            Assert.Equal(ExpectDCHInDECSLRM, s);
        }

        //  "000000000111111111122222222223333333333444444444455555555556666666666777777777788
        //  "123456789012345678901234567890123456789012345678901234567890123456789012345678901"
        //  "ABCdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab " // Before
        //  "A Cdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab " // CUB(2).ECH()
        //  "A   efghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab " // ECH(3)
        //  "A                                                                                " // ECH(100)
        public static readonly string ExpectECH =
            "A Cdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab ";
        public static readonly string ExpectECH3 =
            "A   efghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab ";
        public static readonly string ExpectECH100 =
            "A                                                                                ";

        // !ECH
        // RESET
        //   erase 0..25,0..80
        //   ?cursor = 0,0
        // PUSH "ABC"
        // PUSH "\e[2D"
        //   ?cursor = 0,1
        // PUSH "\e[X"
        //   erase 0..1,1..2
        //   ?cursor = 0,1
        // PUSH "\e[3X"
        //   erase 0..1,1..4
        //   ?cursor = 0,1
        // # ECH more columns than there are should be bounded
        // PUSH "\e[100X"
        //   erase 0..1,1..80
        [Fact]
        public void EraseCharacter()
        {
            var s = string.Empty;
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingDiagonalLower();

            // PUSH "ABC"
            // PUSH "\e[2D"
            //   ?cursor = 0,1
            Push(d, "ABC".CUB(2));
            Assert.True(IsCursor(t, 0, 1));

            // PUSH "\e[X"
            //   erase 0..1,1..2
            //   ?cursor = 0,1
            Push(d, "".ECH());            
            Assert.True(IsCursor(t, 0, 1));
            s = t.GetVisibleChars(0, 0, 81);
            Assert.Equal(ExpectECH, s);

            // PUSH "\e[3X"
            //   erase 0..1,1..4
            //   ?cursor = 0,1
            Push(d, "".ECH(3));
            Assert.True(IsCursor(t, 0, 1));
            s = t.GetVisibleChars(0, 0, 81);
            Assert.Equal(ExpectECH3, s);

            // # ECH more columns than there are should be bounded
            // PUSH "\e[100X"
            //   erase 0..1,1..80
            Push(d, "".ECH(100));
            Assert.True(IsCursor(t, 0, 1));
            s = t.GetVisibleChars(0, 0, 81);
            Assert.Equal(ExpectECH100, s);
        }

        public static readonly string ExpectIL =
            "Abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab" + "\n" +
            "                                                                                " + "\n" +
            "Cabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyza" + "\n" +
            "cbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz" + "\n" +
            "dcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxy" + "\n" +
            "edcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwx" + "\n" +
            "fedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvw" + "\n" +
            "gfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuv" + "\n" +
            "hgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstu" + "\n" +
            "ihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrst" + "\n" +
            "jihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrs" + "\n" +
            "kjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqr" + "\n" +
            "lkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopq" + "\n" +
            "mlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnop" + "\n" +
            "nmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmno" + "\n" +
            "onmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmn" + "\n" +
            "ponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklm" + "\n" +
            "qponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl" + "\n" +
            "rqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk" + "\n" +
            "srqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghij" + "\n" +
            "tsrqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghi" + "\n" +
            "utsrqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefgh" + "\n" +
            "vutsrqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefg" + "\n" +
            "wvutsrqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdef" + "\n" +
            "xwvutsrqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcde"
            ;

        public static readonly string ExpectIL3 =
            "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab" + "\n" +
            "                                                                                " + "\n" +
            "                                                                                " + "\n" +
            "                                                                                " + "\n" +
            "Babcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyza" + "\n" +
            "cbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz" + "\n" +
            "dcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxy" + "\n" +
            "edcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwx" + "\n" +
            "fedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvw" + "\n" +
            "gfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuv" + "\n" +
            "hgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstu" + "\n" +
            "ihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrst" + "\n" +
            "jihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrs" + "\n" +
            "kjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqr" + "\n" +
            "lkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopq" + "\n" +
            "mlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnop" + "\n" +
            "nmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmno" + "\n" +
            "onmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmn" + "\n" +
            "ponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklm" + "\n" +
            "qponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl" + "\n" +
            "rqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk" + "\n" +
            "srqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghij" + "\n" +
            "tsrqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghi" + "\n" +
            "utsrqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefgh" + "\n" +
            "vutsrqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefg";

        // !IL
        // RESET
        //   erase 0..25,0..80
        //   ?cursor = 0,0
        // PUSH "A\r\nC"
        //   ?cursor = 1,1
        // PUSH "\e[L"
        //   scrollrect 1..25,0..80 => -1,+0
        //   # TODO: ECMA-48 says we should move to line home, but neither xterm nor
        //   # xfce4-terminal do this
        //   ?cursor = 1,1
        // PUSH "\rB"
        //   ?cursor = 1,1
        // PUSH "\e[3L"
        //   scrollrect 1..25,0..80 => -3,+0
        [Fact]
        public void InsertLine()
        {
            var s = string.Empty;
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingDiagonalLower();

            // PUSH "A\r\nC"
            //   ?cursor = 1,1
            Push(d, "A".CR().LF().T("C"));
            Assert.True(IsCursor(t, 1, 1));

            // PUSH "\e[L"
            //   scrollrect 1..25,0..80 => -1,+0
            //   # TODO: ECMA-48 says we should move to line home, but neither xterm nor
            //   # xfce4-terminal do this
            //   ?cursor = 1,1
            Push(d, "".IL());
            Assert.True(IsCursor(t, 1, 1));
            s = t.GetScreenText();
            Assert.Equal(ExpectIL, s);

            // PUSH "\rB"
            //   ?cursor = 1,1
            // PUSH "\e[3L"
            //   scrollrect 1..25,0..80 => -3,+0
            t.TestPatternScrollingDiagonalLower();
            Push(d, "\rB".IL(3));
            Assert.True(IsCursor(t, 1, 1));
            s = t.GetScreenText();
            Assert.Equal(ExpectIL3, s);
        }

        public static readonly string ExpectILInDECSTBM =
            "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzab" + "\n" + // 1
            "babcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyza" + "\n" + // 2
            "cbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz" + "\n" + // 3
            "dcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxy" + "\n" + // 4
            "                                                                                " + "\n" + // 5    -
            "edcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwx" + "\n" + // 6
            "fedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvw" + "\n" + // 7
            "gfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuv" + "\n" + // 8
            "hgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstu" + "\n" + // 9
            "ihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrst" + "\n" + // 10
            "jihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrs" + "\n" + // 11
            "kjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqr" + "\n" + // 12
            "lkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopq" + "\n" + // 13
            "mlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnop" + "\n" + // 14
            "nmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmno" + "\n" + // 15   -
            "ponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklm" + "\n" + // 16
            "qponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl" + "\n" + // 17
            "rqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk" + "\n" + // 18
            "srqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghij" + "\n" + // 19
            "tsrqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghi" + "\n" + // 20
            "utsrqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefgh" + "\n" + // 21
            "vutsrqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefg" + "\n" + // 22
            "wvutsrqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdef" + "\n" + // 23
            "xwvutsrqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcde" + "\n" + // 24
            "yxwvutsrqponmlkjihgfedcbabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcd";         // 25

        // !IL with DECSTBM
        // PUSH "\e[5;15r"
        // PUSH "\e[5H\e[L"
        //   scrollrect 4..15,0..80 => -1,+0
        [Fact]
        public void InsertLineInDECSTBM()
        {
            var s = string.Empty;
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingDiagonalLower();

            // PUSH "\e[5;15r"
            // PUSH "\e[5H\e[L"
            //   scrollrect 4..15,0..80 => -1,+0
            Push(d, "".STBM(5,15).CUP(5).IL());
            s = t.GetScreenText();
            Assert.Equal(ExpectILInDECSTBM, s);
        }

        // !IL outside DECSTBM
        // PUSH "\e[20H\e[L"
        //   # nothing happens
        [Fact]
        public void InsertLineOutsideDECSTBM()
        {
            var s = string.Empty;
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingDiagonalLower();

            // PUSH "\e[5;15r"
            // PUSH "\e[5H\e[L"
            //   scrollrect 4..15,0..80 => -1,+0
            Push(d, "".STBM(5, 15).CUP(5).IL().CUP(20).IL());
            s = t.GetScreenText();
            Assert.Equal(ExpectILInDECSTBM, s);
        }

        //  "000000000111111111122222222223333333333444444444455555555556666666666777777777788
        //  "123456789012345678901234567890123456789012345678901234567890123456789012345678901"
        //            |                                       |                               " Margins
        public static readonly string ExpectILInDECSTBMAndDECSLRM =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" + "\n" + // 1
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" + "\n" + // 2
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc" + "\n" + // 3
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd" + "\n" + // 4
            "eeeeeeeee                                         eeeeeeeeeeeeeeeeeeeeeeeeeeeeee" + "\n" + // 5    -
            "fffffffffeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeffffffffffffffffffffffffffffff" + "\n" + // 6
            "gggggggggfffffffffffffffffffffffffffffffffffffffffgggggggggggggggggggggggggggggg" + "\n" + // 7
            "hhhhhhhhhggggggggggggggggggggggggggggggggggggggggghhhhhhhhhhhhhhhhhhhhhhhhhhhhhh" + "\n" + // 8
            "iiiiiiiiihhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhiiiiiiiiiiiiiiiiiiiiiiiiiiiiii" + "\n" + // 9
            "jjjjjjjjjiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiijjjjjjjjjjjjjjjjjjjjjjjjjjjjjj" + "\n" + // 10
            "kkkkkkkkkjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" + "\n" + // 11
            "lllllllllkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkllllllllllllllllllllllllllllll" + "\n" + // 12
            "mmmmmmmmmlllllllllllllllllllllllllllllllllllllllllmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm" + "\n" + // 13
            "nnnnnnnnnmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn" + "\n" + // 14
            "ooooooooonnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnoooooooooooooooooooooooooooooo" + "\n" + // 15   -
            "pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp" + "\n" + // 16
            "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" + "\n" + // 17
            "rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr" + "\n" + // 18
            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" + "\n" + // 19
            "tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt" + "\n" + // 20
            "uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu" + "\n" + // 21
            "vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv" + "\n" + // 22
            "wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww" + "\n" + // 23
            "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" + "\n" + // 24
            "yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy";         // 25

        //  "000000000111111111122222222223333333333444444444455555555556666666666777777777788
        //  "123456789012345678901234567890123456789012345678901234567890123456789012345678901"
        //            |                                       |                               " Margins
        public static readonly string ExpectIL5InDECSTBMAndDECSLRM =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" + "\n" + // 1
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" + "\n" + // 2
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc" + "\n" + // 3
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd" + "\n" + // 4
            "eeeeeeeee                                         eeeeeeeeeeeeeeeeeeeeeeeeeeeeee" + "\n" + // 5    -
            "fffffffff                                         ffffffffffffffffffffffffffffff" + "\n" + // 6
            "ggggggggg                                         gggggggggggggggggggggggggggggg" + "\n" + // 7
            "hhhhhhhhh                                         hhhhhhhhhhhhhhhhhhhhhhhhhhhhhh" + "\n" + // 8
            "iiiiiiiii                                         iiiiiiiiiiiiiiiiiiiiiiiiiiiiii" + "\n" + // 9
            "jjjjjjjjjeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeejjjjjjjjjjjjjjjjjjjjjjjjjjjjjj" + "\n" + // 10
            "kkkkkkkkkfffffffffffffffffffffffffffffffffffffffffkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" + "\n" + // 11
            "lllllllllgggggggggggggggggggggggggggggggggggggggggllllllllllllllllllllllllllllll" + "\n" + // 12
            "mmmmmmmmmhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm" + "\n" + // 13
            "nnnnnnnnniiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiinnnnnnnnnnnnnnnnnnnnnnnnnnnnnn" + "\n" + // 14
            "ooooooooojjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjoooooooooooooooooooooooooooooo" + "\n" + // 15   -
            "pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp" + "\n" + // 16
            "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" + "\n" + // 17
            "rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr" + "\n" + // 18
            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" + "\n" + // 19
            "tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt" + "\n" + // 20
            "uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu" + "\n" + // 21
            "vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv" + "\n" + // 22
            "wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww" + "\n" + // 23
            "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" + "\n" + // 24
            "yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy";         // 25

        //  "000000000111111111122222222223333333333444444444455555555556666666666777777777788
        //  "123456789012345678901234567890123456789012345678901234567890123456789012345678901"
        //            |                                       |                               " Margins
        public static readonly string ExpectIL100InDECSTBMAndDECSLRM =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" + "\n" + // 1
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" + "\n" + // 2
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc" + "\n" + // 3
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd" + "\n" + // 4
            "eeeeeeeee                                         eeeeeeeeeeeeeeeeeeeeeeeeeeeeee" + "\n" + // 5    -
            "fffffffff                                         ffffffffffffffffffffffffffffff" + "\n" + // 6
            "ggggggggg                                         gggggggggggggggggggggggggggggg" + "\n" + // 7
            "hhhhhhhhh                                         hhhhhhhhhhhhhhhhhhhhhhhhhhhhhh" + "\n" + // 8
            "iiiiiiiii                                         iiiiiiiiiiiiiiiiiiiiiiiiiiiiii" + "\n" + // 9
            "jjjjjjjjj                                         jjjjjjjjjjjjjjjjjjjjjjjjjjjjjj" + "\n" + // 10
            "kkkkkkkkk                                         kkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" + "\n" + // 11
            "lllllllll                                         llllllllllllllllllllllllllllll" + "\n" + // 12
            "mmmmmmmmm                                         mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm" + "\n" + // 13
            "nnnnnnnnn                                         nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn" + "\n" + // 14
            "ooooooooo                                         oooooooooooooooooooooooooooooo" + "\n" + // 15   -
            "pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp" + "\n" + // 16
            "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" + "\n" + // 17
            "rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr" + "\n" + // 18
            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" + "\n" + // 19
            "tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt" + "\n" + // 20
            "uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu" + "\n" + // 21
            "vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv" + "\n" + // 22
            "wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww" + "\n" + // 23
            "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" + "\n" + // 24
            "yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy";         // 25

        // !IL with DECSTBM+DECSLRM
        // PUSH "\e[?69h"
        // PUSH "\e[10;50s"
        // PUSH "\e[5;10H\e[L"
        //   scrollrect 4..15,9..50 => -1,+0
        [Fact]
        public void InsertLineInDECSTBMAndDECSLRM()
        {
            var s = string.Empty;
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingLower();

            // Carried over from InsertLineOutsideDECSTBM
            Push(d, "".STBM(5, 15));

            // PUSH "\e[?69h"
            // PUSH "\e[10;50s"
            // PUSH "\e[5;10H\e[L"
            //   scrollrect 4..15,9..50 => -1,+0
            Push(d, "".EnableLRMM().LRMM(10,50).CUP(5,10).IL());
            s = t.GetScreenText();
            Assert.Equal(ExpectILInDECSTBMAndDECSLRM, s);

            // Added bonus, try scrolling 5 lines
            t.TestPatternScrollingLower();
            Push(d, "".IL(5));
            s = t.GetScreenText();
            Assert.Equal(ExpectIL5InDECSTBMAndDECSLRM, s);

            // Added bonus, try scrolling 100 lines
            t.TestPatternScrollingLower();
            Push(d, "".IL(100));
            s = t.GetScreenText();
            Assert.Equal(ExpectIL100InDECSTBMAndDECSLRM, s);
        }

        //  "000000000111111111122222222223333333333444444444455555555556666666666777777777788
        //  "123456789012345678901234567890123456789012345678901234567890123456789012345678901"
        public static readonly string ExpectDL =
            "Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" + "\n" + // 1
            "Bccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc" + "\n" + // 2
            "Cddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd" + "\n" + // 3
            "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee" + "\n" + // 4
            "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" + "\n" + // 5 
            "gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg" + "\n" + // 6
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh" + "\n" + // 7
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii" + "\n" + // 8
            "jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj" + "\n" + // 9
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" + "\n" + // 10
            "llllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllll" + "\n" + // 11
            "mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm" + "\n" + // 12
            "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn" + "\n" + // 13
            "oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo" + "\n" + // 14
            "pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp" + "\n" + // 15
            "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" + "\n" + // 16
            "rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr" + "\n" + // 17
            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" + "\n" + // 18
            "tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt" + "\n" + // 19
            "uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu" + "\n" + // 20
            "vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv" + "\n" + // 21
            "wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww" + "\n" + // 22
            "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" + "\n" + // 23
            "yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy" + "\n" + // 24
            "                                                                                ";         // 25

        //  "000000000111111111122222222223333333333444444444455555555556666666666777777777788
        //  "123456789012345678901234567890123456789012345678901234567890123456789012345678901"
        public static readonly string ExpectDL3 =
            "Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" + "\n" + // 1
            "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" + "\n" + // 2
            "gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg" + "\n" + // 3
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh" + "\n" + // 4
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii" + "\n" + // 5 
            "jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj" + "\n" + // 6
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" + "\n" + // 7
            "llllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllll" + "\n" + // 8
            "mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm" + "\n" + // 9
            "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn" + "\n" + // 10
            "oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo" + "\n" + // 11
            "pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp" + "\n" + // 12
            "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" + "\n" + // 13
            "rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr" + "\n" + // 14
            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" + "\n" + // 15
            "tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt" + "\n" + // 16
            "uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu" + "\n" + // 17
            "vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv" + "\n" + // 18
            "wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww" + "\n" + // 19
            "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" + "\n" + // 20
            "yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy" + "\n" + // 21
            "                                                                                " + "\n" + // 22
            "                                                                                " + "\n" + // 23
            "                                                                                " + "\n" + // 24
            "                                                                                ";         // 25

        // !DL
        // RESET
        //   erase 0..25,0..80
        //   ?cursor = 0,0
        // PUSH "A\r\nB\r\nB\r\nC"
        //   ?cursor = 3,1
        // PUSH "\e[2H"
        //   ?cursor = 1,0
        // PUSH "\e[M"
        //   scrollrect 1..25,0..80 => +1,+0
        //   ?cursor = 1,0
        // PUSH "\e[3M"
        //   scrollrect 1..25,0..80 => +3,+0
        //   ?cursor = 1,0
        [Fact]
        public void DeleteLines()
        {
            var s = string.Empty;
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingLower();

            // PUSH "A\r\nB\r\nB\r\nC"
            //   ?cursor = 3,1
            Push(d, "A".CR().LF().T("B").CR().LF().T("B").CR().LF().T("C"));
            Assert.True(IsCursor(t, 3, 1));

            // PUSH "\e[2H"
            //   ?cursor = 1,0
            Push(d, "".CUP(2));
            Assert.True(IsCursor(t, 1, 0));

            // PUSH "\e[M"
            //   scrollrect 1..25,0..80 => +1,+0
            //   ?cursor = 1,0
            Push(d, "".DL());
            s = t.GetScreenText();
            Assert.Equal(ExpectDL, s);

            // PUSH "\e[3M"
            //   scrollrect 1..25,0..80 => +3,+0
            //   ?cursor = 1,0
            Push(d, "".DL(3));
            s = t.GetScreenText();
            Assert.Equal(ExpectDL3, s);
        }

        //  "000000000111111111122222222223333333333444444444455555555556666666666777777777788
        //  "123456789012345678901234567890123456789012345678901234567890123456789012345678901"
        public static readonly string ExpectDLWithDECSTTBM =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" + "\n" + // 1
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" + "\n" + // 2
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc" + "\n" + // 3
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd" + "\n" + // 4
            "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" + "\n" + // 5    -
            "gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg" + "\n" + // 6
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh" + "\n" + // 7
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii" + "\n" + // 8
            "jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj" + "\n" + // 9
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" + "\n" + // 10
            "llllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllll" + "\n" + // 11
            "mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm" + "\n" + // 12
            "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn" + "\n" + // 13
            "oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo" + "\n" + // 14
            "                                                                                " + "\n" + // 15   -
            "pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp" + "\n" + // 16
            "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" + "\n" + // 17
            "rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr" + "\n" + // 18
            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" + "\n" + // 19
            "tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt" + "\n" + // 20
            "uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu" + "\n" + // 21
            "vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv" + "\n" + // 22
            "wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww" + "\n" + // 23
            "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" + "\n" + // 24
            "yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy";         // 25

        // !DL with DECSTBM
        // PUSH "\e[5;15r"
        // PUSH "\e[5H\e[M"
        //   scrollrect 4..15,0..80 => +1,+0
        [Fact]
        public void DeleteLinesWithDECSTTBM()
        {
            var s = string.Empty;
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingLower();

            // PUSH "\e[5;15r"
            // PUSH "\e[5H\e[M"
            Push(d, "".STBM(5,15).CUP(5).DL());
            s = t.GetScreenText();
            Assert.Equal(ExpectDLWithDECSTTBM, s);
        }

        //  "000000000111111111122222222223333333333444444444455555555556666666666777777777788
        //  "123456789012345678901234567890123456789012345678901234567890123456789012345678901"
        public static readonly string ExpectDLOusideDECSTTBM =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" + "\n" + // 1
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" + "\n" + // 2
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc" + "\n" + // 3
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd" + "\n" + // 4
            "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee" + "\n" + // 5    -
            "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" + "\n" + // 6
            "gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg" + "\n" + // 7
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh" + "\n" + // 8
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii" + "\n" + // 9
            "jjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj" + "\n" + // 10
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" + "\n" + // 11
            "llllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllll" + "\n" + // 12
            "mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm" + "\n" + // 13
            "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn" + "\n" + // 14
            "oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo" + "\n" + // 15   -
            "pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp" + "\n" + // 16
            "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" + "\n" + // 17
            "rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr" + "\n" + // 18
            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" + "\n" + // 19
            "tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt" + "\n" + // 20
            "uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu" + "\n" + // 21
            "vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv" + "\n" + // 22
            "wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww" + "\n" + // 23
            "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" + "\n" + // 24
            "yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy";         // 25
        
        // !DL outside DECSTBM
        // PUSH "\e[20H\e[M"
        //   # nothing happens
        [Fact]
        public void DeleteLinesOutsideDECSTTBM()
        {
            var s = string.Empty;
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingLower();

            // PUSH "\e[5;15r"
            // PUSH "\e[20H\e[M"
            //   # nothing happens
            Push(d, "".STBM(5, 15).CUP(20).DL());
            s = t.GetScreenText();
            Assert.Equal(ExpectDLOusideDECSTTBM, s);
        }

        //  "000000000111111111122222222223333333333444444444455555555556666666666777777777788
        //  "123456789012345678901234567890123456789012345678901234567890123456789012345678901"
        //            |                                       |                               " Margins
        public static readonly string ExpectDLInDECSTTBMAndDECSLRM =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" + "\n" + // 1
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" + "\n" + // 2
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc" + "\n" + // 3
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd" + "\n" + // 4
            "eeeeeeeeefffffffffffffffffffffffffffffffffffffffffeeeeeeeeeeeeeeeeeeeeeeeeeeeeee" + "\n" + // 5    -
            "fffffffffgggggggggggggggggggggggggggggggggggggggggffffffffffffffffffffffffffffff" + "\n" + // 6
            "ggggggggghhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhgggggggggggggggggggggggggggggg" + "\n" + // 7
            "hhhhhhhhhiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiihhhhhhhhhhhhhhhhhhhhhhhhhhhhhh" + "\n" + // 8
            "iiiiiiiiijjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjjiiiiiiiiiiiiiiiiiiiiiiiiiiiiii" + "\n" + // 9
            "jjjjjjjjjkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkjjjjjjjjjjjjjjjjjjjjjjjjjjjjjj" + "\n" + // 10
            "kkkkkkkkklllllllllllllllllllllllllllllllllllllllllkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" + "\n" + // 11
            "lllllllllmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmllllllllllllllllllllllllllllll" + "\n" + // 12
            "mmmmmmmmmnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm" + "\n" + // 13
            "nnnnnnnnnooooooooooooooooooooooooooooooooooooooooonnnnnnnnnnnnnnnnnnnnnnnnnnnnnn" + "\n" + // 14
            "ooooooooo                                         oooooooooooooooooooooooooooooo" + "\n" + // 15   -
            "pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp" + "\n" + // 16
            "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" + "\n" + // 17
            "rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr" + "\n" + // 18
            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" + "\n" + // 19
            "tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt" + "\n" + // 20
            "uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu" + "\n" + // 21
            "vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv" + "\n" + // 22
            "wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww" + "\n" + // 23
            "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" + "\n" + // 24
            "yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy";         // 25

        // !DL with DECSTBM+DECSLRM
        // PUSH "\e[?69h"
        // PUSH "\e[10;50s"
        // PUSH "\e[5;10H\e[M"
        //   scrollrect 4..15,9..50 => +1,+0
        [Fact]
        public void DeleteLinesInDECSTTBMAndDECSLRM()
        {
            var s = string.Empty;
            var t = new VirtualTerminalController();
            var d = new DataConsumer(t);
            t.ResizeView(80, 25);
            t.TestPatternScrollingLower();

            // PUSH "\e[?69h"
            // PUSH "\e[10;50s"
            // PUSH "\e[5;10H\e[M"
            //   scrollrect 4..15,9..50 => +1,+0
            Push(d, "".STBM(5, 15).EnableLRMM().LRMM(10,50).CUP(5,10).DL());
            s = t.GetScreenText();
            Assert.Equal(ExpectDLInDECSTTBMAndDECSLRM, s);
        }

        // !DECIC
        // RESET
        //   erase 0..25,0..80
        // PUSH "\e[20G\e[5'}"
        //   scrollrect 0..25,19..80 => +0,-5
        // 
        // !DECIC with DECSTBM+DECSLRM
        // PUSH "\e[?69h"
        // PUSH "\e[4;20r\e[20;60s"
        // PUSH "\e[4;20H\e[3'}"
        //   scrollrect 3..20,19..60 => +0,-3
        // 
        // !DECIC outside DECSLRM
        // PUSH "\e[70G\e['}"
        //   # nothing happens
        // 
        // !DECDC
        // RESET
        //   erase 0..25,0..80
        // PUSH "\e[20G\e[5'~"
        //   scrollrect 0..25,19..80 => +0,+5
        // 
        // !DECDC with DECSTBM+DECSLRM
        // PUSH "\e[?69h"
        // PUSH "\e[4;20r\e[20;60s"
        // PUSH "\e[4;20H\e[3'~"
        //   scrollrect 3..20,19..60 => +0,+3
        // 
        // !DECDC outside DECSLRM
        // PUSH "\e[70G\e['~"
        //   # nothing happens
        // 
        // !EL 0
        // RESET
        //   erase 0..25,0..80
        //   ?cursor = 0,0
        // PUSH "ABCDE"
        // PUSH "\e[3D"
        //   ?cursor = 0,2
        // PUSH "\e[0K"
        //   erase 0..1,2..80
        //   ?cursor = 0,2
        // 
        // !EL 1
        // RESET
        //   erase 0..25,0..80
        //   ?cursor = 0,0
        // PUSH "ABCDE"
        // PUSH "\e[3D"
        //   ?cursor = 0,2
        // PUSH "\e[1K"
        //   erase 0..1,0..3
        //   ?cursor = 0,2
        // 
        // !EL 2
        // RESET
        //   erase 0..25,0..80
        //   ?cursor = 0,0
        // PUSH "ABCDE"
        // PUSH "\e[3D"
        //   ?cursor = 0,2
        // PUSH "\e[2K"
        //   erase 0..1,0..80
        //   ?cursor = 0,2
        // 
        // !SEL
        // RESET
        //   erase 0..25,0..80
        //   ?cursor = 0,0
        // PUSH "\e[11G"
        //   ?cursor = 0,10
        // PUSH "\e[?0K"
        //   erase 0..1,10..80 selective
        //   ? cursor = 0,10
        // PUSH "\e[?1K"
        //   erase 0..1,0..11 selective
        //   ? cursor = 0,10
        // PUSH "\e[?2K"
        //   erase 0..1,0..80 selective
        //   ? cursor = 0,10
        // 
        // !ED 0
        // RESET
        //   erase 0..25,0..80
        //   ?cursor = 0,0
        // PUSH "\e[2;2H"
        //   ?cursor = 1,1
        // PUSH "\e[0J"
        //   erase 1..2,1..80
        //   erase 2..25,0..80
        //   ?cursor = 1,1
        // 
        // !ED 1
        // RESET
        //   erase 0..25,0..80
        //   ?cursor = 0,0
        // PUSH "\e[2;2H"
        //   ?cursor = 1,1
        // PUSH "\e[1J"
        //   erase 0..1,0..80
        //   erase 1..2,0..2
        //   ?cursor = 1,1
        // 
        // !ED 2
        // RESET
        //   erase 0..25,0..80
        //   ?cursor = 0,0
        // PUSH "\e[2;2H"
        //   ?cursor = 1,1
        // PUSH "\e[2J"
        //   erase 0..25,0..80
        //   ?cursor = 1,1
        // 
        // !SED
        // RESET
        //   erase 0..25,0..80
        // PUSH "\e[5;5H"
        //   ?cursor = 4,4
        // PUSH "\e[?0J"
        //   erase 4..5,4..80 selective
        //   erase 5..25,0..80 selective
        //   ? cursor = 4,4
        // PUSH "\e[?1J"
        //   erase 0..4,0..80 selective
        //   erase 4..5,0..5 selective
        //   ? cursor = 4,4
        // PUSH "\e[?2J"
        //   erase 0..25,0..80 selective
        //   ? cursor = 4,4
        // 
        // !DECRQSS on DECSCA
        // PUSH "\e[2\"q"
        // PUSH "\eP\$q\"q\e\\"
        //   output "\eP1\$r2\"q\e\\"
        // 
        // WANTSTATE -s+m
        // 
        // !ICH move+erase emuation
        // RESET
        //   erase 0..25,0..80
        //   ?cursor = 0,0
        // PUSH "ACD"
        // PUSH "\e[2D"
        //   ?cursor = 0,1
        // PUSH "\e[@"
        //   moverect 0..1,1..79 -> 0..1,2..80
        //   erase 0..1,1..2
        //   ?cursor = 0,1
        // PUSH "B"
        //   ?cursor = 0,2
        // PUSH "\e[3@"
        //   moverect 0..1,2..77 -> 0..1,5..80
        //   erase 0..1,2..5
        // 
        // !DCH move+erase emulation
        // RESET
        //   erase 0..25,0..80
        //   ?cursor = 0,0
        // PUSH "ABBC"
        // PUSH "\e[3D"
        //   ?cursor = 0,1
        // PUSH "\e[P"
        //   moverect 0..1,2..80 -> 0..1,1..79
        //   erase 0..1,79..80
        //   ?cursor = 0,1
        // PUSH "\e[3P"
        //   moverect 0..1,4..80 -> 0..1,1..77
        //   erase 0..1,77..80
        //   ?cursor = 0,1

    }
}
