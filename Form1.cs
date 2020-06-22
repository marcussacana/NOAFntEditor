using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Windows.Media;

namespace NOAFntEditor
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        int FontTblOffset;
        uint FontEntryCount;
        int FontSize;

        string EXEPath;
        string PNGPath;
        Glyph[] Glyphs;

        Bitmap Texture;
        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog();
            fd.Title = "Select the NOA Main Executable";
            fd.Filter = "NOA MAIN EXE|CNN.exe";
            if (fd.ShowDialog() != DialogResult.OK)
                return;

            var Desc = FileVersionInfo.GetVersionInfo(fd.FileName).FileDescription;
            if (Desc != "Nights of Azure")
            {
                MessageBox.Show("Please, select the Nights of Azure Execcutable, this tool is not enough stable to works with others game of the same engine.", "NOA Font Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            EXEPath = fd.FileName;

            fd.Title = "Select the Font Texture";
            fd.Filter = "All PNG Files|*.png";
            if (fd.ShowDialog() != DialogResult.OK)
                return;

            PNGPath = fd.FileName;
            using (Stream IMG = new StreamReader(PNGPath).BaseStream)
            using (Stream Stream = new StreamReader(EXEPath).BaseStream)
            {
                Texture = Image.FromStream(IMG) as Bitmap;
                Glyphs = GetGlyphs(Stream);
            }

            var Importants = GetImportants();
            var BestRemaps = (from x in Glyphs where !tbRequiredChars.Text.Contains(x.Char) && !Importants.Contains(x.Char) orderby x.Width descending select x).ToArray();

            string NoAccents = RemoveDiacritics(tbRequiredChars.Text);
            string FakeChars = new string('-', NoAccents.Length);
            for (int i = 0, x = 0; i < tbRequiredChars.Text.Length; i++) {
                if (tbRequiredChars.Text[i] == NoAccents[i])
                    continue;

                FakeChars = ReplaceAt(i, FakeChars, BestRemaps[x++].Char.ToString());
            }
            tbRemap.Text = FakeChars;
            textBox4.Text = "36,0";
            PreviewText();
        }

        private Glyph[] GetGlyphs(Stream Stream) {
            FontTblOffset = FindFontTable(Stream);
            if (FontTblOffset == -1)
                throw new Exception("Failed to Find the Font Table");

            var Question = new Form2("How Many Characters in the font?", 0xC4B);
            Question.ShowDialog();
            FontEntryCount = Question.Value;

            Question = new Form2("What is the Font Size?", 64);
            Question.ShowDialog();
            FontSize = (int)Question.Value;

            Text = "Reading Glyphs Table...";

            Stream.Position = FontTblOffset;
            using (BinaryReader Reader = new BinaryReader(Stream, Encoding.UTF8))
            {
                List<Glyph> Glyphs = new List<Glyph>();
                for (int i = 0; i < FontEntryCount; i++)
                {
                    var Glyph = new Glyph()
                    {
                        UTF8 = Reader.ReadUInt32(),
                        X = Reader.ReadUInt16(),
                        Y = Reader.ReadUInt16(),
                        Width = Reader.ReadUInt16(),
                        Height = Reader.ReadUInt16(),
                        PaddingLeft = Reader.ReadInt32(),
                        PaddingTop = Reader.ReadInt32(),
                        PaddingRigth = Reader.ReadInt32(),
                        PaddingBottom = Reader.ReadInt32()
                    };
                    Glyphs.Add(Glyph);
					Application.DoEvents();
                }

                for (int i = 0; i < Glyphs.Count; i++) {
                    var Glyph = Glyphs[i];

                    Bitmap CharTex = new Bitmap(Glyph.Width, Glyph.Height);
                    CopyBitmap(Texture, CharTex, Glyph.X, Glyph.Y, Glyph.Width, Glyph.Height, 0, 0);

                    Glyph.Texture = CharTex;
                    Glyphs[i] = Glyph;
					Application.DoEvents();
                }

                Text = "NOA Font Editor";
                return Glyphs.ToArray();
            }
        }

        private void UpdateGlyphs(Glyph[] Glyphs) {
            if (!File.Exists(EXEPath + ".bak"))
                File.Copy(EXEPath, EXEPath + ".bak");

            if (!File.Exists(PNGPath + ".bak"))
                File.Copy(PNGPath, PNGPath + ".bak");

            using (Bitmap NewTexture = new Bitmap(Texture.Width, Texture.Height))
            using (Stream Stream = File.OpenWrite(EXEPath))
            using (BinaryWriter Writer = new BinaryWriter(Stream, Encoding.UTF8))
            {
                Stream.Position = FontTblOffset;
                foreach (var Glyph in Glyphs) {
                    Writer.Write(Glyph.UTF8);
                    Writer.Write(Glyph.X);
                    Writer.Write(Glyph.Y);
                    Writer.Write(Glyph.Width);
                    Writer.Write(Glyph.Height);
                    Writer.Write(Glyph.PaddingLeft);
                    Writer.Write(Glyph.PaddingTop);
                    Writer.Write(Glyph.PaddingRigth);
                    Writer.Write(Glyph.PaddingBottom);
					Application.DoEvents();
                }

                for (int i = 0; i < Glyphs.Length; i++)
                {
                    var Glyph = Glyphs[i];
                    CopyBitmap(Glyph.Texture, NewTexture, 0, 0, Glyph.Texture.Width, Glyph.Texture.Height, Glyph.X, Glyph.Y);
					Application.DoEvents();
                }

                foreach (var i in Overrided) {
                    var Glyph = Glyphs[i];
                    CopyBitmap(Glyph.Texture, NewTexture, 0, 0, Glyph.Texture.Width, Glyph.Texture.Height, Glyph.X, Glyph.Y);
                }

                NewTexture.Save(PNGPath, ImageFormat.Png);
            }
        }

        private void CopyBitmap(Bitmap From, Bitmap To, int SourceX, int SourceY, int Width, int Height, int DestX, int DestY) {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    int XDest = DestX + x;
                    int YDest = DestY + y;
                    if (XDest < 0 || YDest < 0)
                        continue;

                    if (XDest >= To.Width || YDest >= To.Height)
                        continue;

                    var Pixel = From.GetPixel(SourceX + x, SourceY + y);
                    To.SetPixel(XDest, YDest, Pixel);
                }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            timer1.Stop();
            timer1.Start();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Stop();
            PreviewText();
        }

        private void PreviewText()
        {
            if (tbTestText.Text.Length == 0)
                return;

            Bitmap Texture = new Bitmap(1,  1);
            for (int i = 0; i < tbTestText.Text.Length; i++)
            {
                char c = tbTestText.Text[i];
                Glyph Glyph = (from x in Glyphs where x.Char == c select x).FirstOrDefault();
                if (Glyph.Char == '\x0') {
                    continue;
                }


                Point DrawAt = Point.Empty;
                DrawAt.Y += Glyph.PaddingTop;
                DrawAt.X += Glyph.PaddingLeft;

                Texture = SideBySide(Texture, Glyph.Texture, DrawAt);
            }

            pictureBox1.Image = Texture;
        }

        private Bitmap SideBySide(Bitmap A, Bitmap B, Point DrawAt) {
            Bitmap Output = new Bitmap(A.Width + B.Width + DrawAt.X, Math.Max(A.Height, B.Height + DrawAt.Y));
            CopyBitmap(A, Output, 0, 0, A.Width, A.Height, 0, 0);
            CopyBitmap(B, Output, 0, 0, B.Width, B.Height, A.Width + DrawAt.X, DrawAt.Y);
            return Output;
        }

        uint? GamepadIndex = null;
        private string GetImportants() {
            string Important = " Э";
            if (GamepadIndex == null)
            {
                var Question = new Form2("What is the position of the gamepad buttons in the glyph table?", 144);
                Question.ShowDialog();
               GamepadIndex = Question.Value;
            }

            for (int i = 0; i < 29; i++)
                Important += Glyphs[GamepadIndex.Value + i].Char;
            return Important;
        }

        List<int> Overrided;
        private void button3_Click(object sender, EventArgs e)
        {
            if (tbRequiredChars.Text.Length > Glyphs.Length)
                throw new Exception("Too many glyphs in the list");

            if (tbRemap.Text.Length != tbRequiredChars.Text.Length) {
                MessageBox.Show("The Remap textbox needs have the same amount of characters of the 'chars' textbox\nChars that you don't need remap put a - in the same position.");
                return;
            }

            Overrided = new List<int>();
            string Important = GetImportants();
            foreach (var c in Important) {
                Overrided.Add(GetGlyphIndex(c));
            }

            var RemapList = "";
            var FSize = float.Parse(textBox4.Text);
            var Font = new Font(textBox3.Text, FSize, FontStyle.Regular, GraphicsUnit.Pixel);
            for (int i = 0; i < tbRequiredChars.Text.Length; i++)
            {
                Size NewSize;
                char VisibleChar = tbRequiredChars.Text[i];
                char RealChar = tbRemap.Text[i];
                if (RealChar == '-')
                    RealChar = VisibleChar;
                else
                    RemapList += $"{VisibleChar}={RealChar}\n";

                using (Graphics tmp = Graphics.FromHwnd(IntPtr.Zero)) {
                    tmp.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    NewSize = tmp.MeasureString(VisibleChar.ToString(), Font).ToSize();
                }

                var Buffer = new Bitmap(NewSize.Width, NewSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(Buffer))
                {
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    g.DrawString(VisibleChar.ToString(), Font, System.Drawing.Brushes.White, 0, 0);
                    g.Flush();
                    g.Dispose();
                }

                if (char.IsWhiteSpace(VisibleChar))
                    continue;

                var Trimmed = TrimBitmap(Buffer);
                if (Trimmed != null)
                    NewSize = Trimmed.Size;

                int x = GetGlyphIndex(RealChar);

                Glyph? OriPadding = null;
                if (x != -1 && RealChar == VisibleChar)
                    OriPadding = Glyphs[x];
                else {
                    var tmp = GetGlyphIndex(RemoveDiacritics(VisibleChar));
                    if (tmp != -1)
                        OriPadding = Glyphs[tmp];
                }

                //If char is very small
                if (x != -1 && (Glyphs[x].Height < NewSize.Height || Glyphs[x].Width < NewSize.Width)) {
                    for (int y = 0; y < Glyphs.Length; y++)
                    {
                        if (Glyphs[y].Height < NewSize.Height || Glyphs[y].Width < NewSize.Width || Overrided.Contains(y))
                            continue;
                        if (tbRequiredChars.Text.Contains(Glyphs[y].Char))
                            continue;

                        Glyphs[x].X = Glyphs[y].X;
                        Glyphs[x].Y = Glyphs[y].Y;
                        Glyphs[x].Width = Glyphs[y].Width;
                        Glyphs[x].Height = Glyphs[y].Height;
                        Overrided.Add(y);
                        break;
                    }
                }

                //If the char is missing in the font
                if (x == -1) 
                    for (int y = Glyphs.Length - 1; y >= 0; y--)
                    {
                        if (Glyphs[y].Height < NewSize.Height || Glyphs[y].Width < NewSize.Width || Overrided.Contains(y))
                            continue;
                        if (tbRequiredChars.Text.Contains(Glyphs[y].Char))
                            continue;
                        /*
                        if (c < Glyphs.Length && !tbRequiredChars.Text.Contains(Glyphs[VisibleChar].Char)) {
                            x = VisibleChar;
                            Glyphs[x].X = Glyphs[y].X;
                            Glyphs[x].Y = Glyphs[y].Y;
                            Glyphs[x].Width = Glyphs[y].Width;
                            Glyphs[x].Height = Glyphs[y].Height;
                            Overrided.Add(y);
                            break;
                        }*/
                        x = y;
                        break;
                    }

                if (x == -1)
                    throw new Exception("No Avaliable Glyph to be replaced.");

                Overrided.Add(x);

                Glyphs[x].Char = RealChar;

                if (NewSize.Width < Glyphs[x].Width)
                    Glyphs[x].Width = (ushort)NewSize.Width;
                if (NewSize.Height < Glyphs[x].Height)
                    Glyphs[x].Height = (ushort)NewSize.Height;

                if (OriPadding != null)
                {
                    Glyphs[x].PaddingLeft = OriPadding.Value.PaddingLeft;
                    Glyphs[x].PaddingTop = ScanTop(Buffer);
                    Glyphs[x].PaddingRigth = Glyphs[x].Width + 1;
                    Glyphs[x].PaddingBottom = 0;
                }
                else
                {
                    Glyphs[x].PaddingLeft = 0;
                    Glyphs[x].PaddingTop = ScanTop(Buffer);
                    Glyphs[x].PaddingRigth = Glyphs[x].Width + 1;
                    Glyphs[x].PaddingBottom = 0;
                }

                Glyphs[x].Texture = Trimmed ?? Buffer;
            }

            PreviewText();

            MessageBox.Show("The Remap table:\n" + RemapList + "\n==========================\nPress CTRL + C to copy.", "NOAFntEditor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        static int ScanTop(Bitmap Bitmap)
        {
            for (int Y = 0; Y < Bitmap.Height; Y++)
                for (int X = 0; X < Bitmap.Width; X++)
                {
                    var Pixel = Bitmap.GetPixel(X, Y);
                    if (Pixel.A == 0)
                        continue;
                    return Y;
                }
            return 0;
        }
        static char RemoveDiacritics(char Char) => RemoveDiacritics(Char.ToString()).First();
        static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        private int GetGlyphIndex(char c)
        {
            for (int i = 0; i < Glyphs.Length; i++)
            {
                if (Glyphs[i].Char == c)
                    return i;
            }
            return -1;
        }

        int FindFontTable(Stream Exe) {
            bool InAssertion = false;
            Exe.Position = 0;
            byte[] Data = new byte[Exe.Length];
            Exe.Read(Data, 0, Data.Length);

            Text = "Seaching Font Table...";
            for (int i = 0; i + 4 < Data.Length; i++) {
                if (!InAssertion) {
                    if (i % 10 == 0)
                        Application.DoEvents();

                    var DW = GetU32(Data, i);
                    if (DW != 0x746E6F46)//Font
                        continue;
                    var Str = GetString(Data, i);
                    if (Str != "Font/")
                        continue;
                    InAssertion = true;
                    continue;
                }
                else {
                    var DW = GetU32(Data, i);
                    if (DW != 0x08000800)
                        continue;
                    Text = "NOA Font Editor";
                    return i + 0x30;
                }
            }
            Text = "NOA Font Editor";
            return -1;
        }

        static uint GetU32(byte[] Data, int At) {
            byte[] Buffer = new byte[4];
            Array.Copy(Data, At, Buffer, 0, 4);
            return BitConverter.ToUInt32(Buffer, 0);
        }
        static string GetString(byte[] Data, int At)
        {
            List<byte> Buffer = new List<byte>();
            do
            {
                var Byte = Data[Buffer.Count + At];
                if (Byte <= 0)
                    break;
                Buffer.Add((byte)Byte);
            } while (true);

            return Encoding.UTF8.GetString(Buffer.ToArray());
        }


        static Bitmap TrimBitmap(Bitmap source)
        {
            bool Empty = true;
            var srcRect = new Rectangle();
            for (int X = 0; X < source.Width && Empty; X++)
                for (int Y = 0; Y < source.Height; Y++) {
                    var Pixel = source.GetPixel(X, Y);
                    if (Pixel.A == 0)
                        continue;

                    if (X > 1)
                        X -= 2;

                    Empty = false;
                    srcRect.X = X;
                    break;
                }
            
            if (Empty)
                return null;

            Empty = true;            
            for (int X = source.Width - 1; X >= 0 && Empty; X--)
                for (int Y = source.Height - 1; Y >= 0; Y--)
                {
                    var Pixel = source.GetPixel(X, Y);
                    if (Pixel.A == 0)
                        continue;

                    if (X < source.Width - 1)
                        X += 2;

                    Empty = false;
                    srcRect.Width = X - srcRect.X;
                    break;
                }

            if (Empty)
                return null;

            Empty = true;
            for (int Y = 0; Y < source.Height && Empty; Y++)
                for (int X = 0; X < source.Width; X++)
                {
                    var Pixel = source.GetPixel(X, Y);
                    if (Pixel.A == 0)
                        continue;

                    if (Y > 0)
                        Y--;

                    Empty = false;
                    srcRect.Y = Y;
                    break;
                }

            if (Empty)
                return null;

            Empty = true;
            for (int Y = source.Height - 1; Y >= 0 && Empty; Y--)
                for (int X = source.Width - 1; X >= 0; X--)
                {
                    var Pixel = source.GetPixel(X, Y);
                    if (Pixel.A == 0)
                        continue;

                    if (Y < source.Height)
                        Y++;

                    Empty = false;
                    srcRect.Height = Y - srcRect.Y;
                    break;
                }

            if (Empty)
                return null;

            Bitmap dest = new Bitmap(srcRect.Width, srcRect.Height);
            Rectangle destRect = new Rectangle(0, 0, srcRect.Width, srcRect.Height);
            using (Graphics graphics = Graphics.FromImage(dest))
            {
                graphics.DrawImage(source, destRect, srcRect, GraphicsUnit.Pixel);
                graphics.Flush();
            }
            return dest;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            UpdateGlyphs(Glyphs);
            MessageBox.Show("Saved");
        }

        //CNN v1.1: 0x9F9BF0 = Font Table
        struct Glyph
        {
            public uint UTF8;
            public ushort X;
            public ushort Y;
            public ushort Width;
            public ushort Height;
            public int PaddingLeft;
            public int PaddingTop;
            public int PaddingRigth;
            public int PaddingBottom;

            public Bitmap Texture;
            public char Char {
                get {
                    if (UTF8 == 0)
                        return char.MinValue;
                    var Bytes = BitConverter.GetBytes(UTF8);
                    while (Bytes.Last() == 0)
                        Array.Resize(ref Bytes, Bytes.Length - 1);
                    Bytes = Bytes.Reverse().ToArray();

                    return Encoding.UTF8.GetString(Bytes).Single();
                } set {
                    var Bytes = Encoding.UTF8.GetBytes(value.ToString());
                    Bytes = Bytes.Reverse().ToArray();

                    byte[] DW = new byte[4];
                    Bytes.CopyTo(DW, 0);

                    UTF8 = BitConverter.ToUInt32(DW, 0);
                }
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog();
            fd.ShowHelp = true;
            fd.HelpRequest += (a, b) => {
                MessageBox.Show("The Chars.lst is a simple font remap pattern used by the StringReloads, it's a plain text with one 'remap' per line, and every remap follow this format: Ori Character=Fake Character\nSee this sample:\nÃ=ァ\nÁ=ィ\nÀ=ゥ", "NOAFntEditor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            fd.Filter = "Any Chars.lst File|chars.lst";
            if (fd.ShowDialog() != DialogResult.OK)
                return;
            
            List<char> OriList  = new List<char>();
            List<char> FakeList = new List<char>();

            string[] Entries = File.ReadAllLines(fd.FileName);
            foreach (var Entry in Entries) {
                var Remap = Entry.Trim();
                if (Remap.Length != 3)
                    continue;
                char Ori  = Remap[0];
                char Fake = Remap[2];

                if (!tbRequiredChars.Text.Contains(Ori))
                    continue;

                OriList.Add(Ori);
                FakeList.Add(Fake);
            }

            string FakePattern = new string('-', tbRequiredChars.Text.Length);
            for (int i = 0; i < FakePattern.Length; i++)
                if (OriList.Contains(tbRequiredChars.Text[i])) {
                    var Index = OriList.IndexOf(tbRequiredChars.Text[i]);
                    FakePattern = ReplaceAt(i, FakePattern, FakeList[Index].ToString());
                }

            tbRemap.Text = FakePattern;

            List<char> MissingRemaps = new List<char>();
            string NoAccents = RemoveDiacritics(tbRequiredChars.Text);
            for (int i = 0; i < tbRequiredChars.Text.Length; i++) {
                if (tbRequiredChars.Text[i] == NoAccents[i])
                    continue;
                if (FakePattern[i] == '-')
                    MissingRemaps.Add(tbRequiredChars.Text[i]);
            }

            if (MissingRemaps.Count > 0) {
                string Msg = "Some characters don't have a remap, and they are:";
                foreach (var Char in MissingRemaps)
                    Msg += $"\n{Char}=?";
                MessageBox.Show(Msg, "NOAFntEditor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private string ReplaceAt(int Index, string String, string Replace) {
            string PartA = String.Substring(0, Index);
            string PartB = String.Substring(Index + Replace.Length);
            return PartA + Replace + PartB;
        }
    }
}

