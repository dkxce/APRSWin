using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Windows.Forms;

namespace RTTSWin
{
    public class Utils
    {
        public static DialogResult InputNumericBox(string title, string promptText, string commentText, long minvalue, long maxvalue, ref long value)
        {
            Form form = new Form();
            Label label = new Label();
            Label label2 = new Label();
            NumericUpDown sizeBox = new NumericUpDown();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            label2.Text = commentText;
            sizeBox.Minimum = minvalue;
            sizeBox.Maximum = maxvalue;
            sizeBox.Value = value;
            sizeBox.TextAlign = HorizontalAlignment.Right;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Отмена";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(10, 12, 372, 13);
            label2.SetBounds(10, 54, 372, 13);
            sizeBox.SetBounds(12, 30, 372, 20);
            buttonOk.SetBounds(228, 76, 75, 23);
            buttonCancel.SetBounds(309, 76, 75, 23);

            label.AutoSize = true;
            label2.AutoSize = true;
            sizeBox.Anchor = sizeBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, sizeBox, label2, buttonOk, buttonCancel });
            form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterParent;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            value = (long)sizeBox.Value;
            return dialogResult;
        }

        public static DialogResult InputBox(string title, string promptText, ref string value)
        {
            return InputBox(title, promptText, 300, null, ref value);
        }

        public static DialogResult InputBox(string title, string promptText, Regex pattern, ref string value)
        {
            return InputBox(title, promptText, 300, pattern, ref value);
        }

        public static DialogResult InputBox(string title, string promptText, int maxWidth, Regex pattern, ref string value)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Отмена";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(10, 20, 372, 13);
            textBox.SetBounds(12, 36, 372, 20);
            buttonOk.SetBounds(228, 72, 75, 23);
            buttonCancel.SetBounds(309, 72, 75, 23);

            label.AutoSize = true;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            form.ClientSize = new Size(Math.Max(maxWidth, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterParent;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            if (pattern != null)
            {
                patterns.Insert(0, pattern);
                textBox.TextChanged += new EventHandler(textBox_TextChanged);
            };

            DialogResult dialogResult = form.ShowDialog();
            if (pattern != null)
                patterns.RemoveAt(0);
            value = textBox.Text;
            return dialogResult;
        }

        public static DialogResult InputLatLon(string title, string promptText, ref double Lat, ref double Lon)
        {
            Form form = new Form();
            Label label = new Label();
            Label labelat = new Label();
            Label labelon = new Label();
            TextBox textBoxLat = new TextBox();
            TextBox textBoxLon = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            labelat.Text = "Lat:";
            labelon.Text = "Lon:";
            textBoxLat.Text = Lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            textBoxLon.Text = Lon.ToString(System.Globalization.CultureInfo.InvariantCulture);


            buttonOk.Text = "OK";
            buttonCancel.Text = "Отмена";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(10, 10, 372, 13);
            labelat.SetBounds(10, 33, 40, 13);
            labelon.SetBounds(10, 53, 40, 13);
            textBoxLat.SetBounds(52, 30, 332, 20);
            textBoxLon.SetBounds(52, 52, 332, 20);
            buttonOk.SetBounds(228, 82, 75, 23);
            buttonCancel.SetBounds(309, 82, 75, 23);

            label.AutoSize = true;
            textBoxLat.BorderStyle = BorderStyle.FixedSingle;
            textBoxLat.Anchor = textBoxLat.Anchor | AnchorStyles.Right;
            textBoxLon.BorderStyle = BorderStyle.FixedSingle;
            textBoxLon.Anchor = textBoxLat.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 117);
            form.Controls.AddRange(new Control[] { label, labelat, labelon, textBoxLat, textBoxLon, buttonOk, buttonCancel });
            form.ClientSize = new Size(260, form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterParent;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            double.TryParse(textBoxLat.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out Lat);
            double.TryParse(textBoxLon.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out Lon);
            return dialogResult;
        }

        public static DialogResult OutCopyDialog(string title, string prompt, string text)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();

            form.Text = title;
            label.Text = prompt;
            label.Font = new Font(label.Font, FontStyle.Bold);
            textBox.Multiline = true;
            textBox.Text = text;
            textBox.ScrollBars = ScrollBars.Both;
            textBox.ReadOnly = true;
            //textBox.BackColor = Color.White;
            textBox.BorderStyle = BorderStyle.None;

            buttonOk.Text = "OK";
            buttonOk.DialogResult = DialogResult.OK;

            label.SetBounds(8, 10, 372, 13);
            textBox.SetBounds(12, 30, 476, 284);
            buttonOk.SetBounds(198, 325, 75, 24);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(500, 360);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk });
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterParent;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;

            textBox.SelectionLength = 0;
            DialogResult dialogResult = form.ShowDialog();
            return dialogResult;
        }

        private static void textBox_TextChanged(object sender, EventArgs e)
        {
            string v = patterns[0].Match(((TextBox)sender).Text).Value;
            if (v != ((((TextBox)sender).Text)))
                ((TextBox)sender).Text = v;
        }

        private static List<Regex> patterns = new List<Regex>();

        public static DialogResult CopyBox(string title, string promptText, int maxWidth, string value)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;
            Color d = textBox.BackColor;
            textBox.ReadOnly = true;
            textBox.BackColor = d;


            buttonCancel.Text = "OK";
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(10, 20, 372, 13);
            textBox.SetBounds(12, 36, 372, 20);
            buttonCancel.SetBounds(309, 72, 75, 23);

            label.AutoSize = true;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonCancel });
            form.ClientSize = new Size(Math.Max(maxWidth, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterParent;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            return dialogResult;
        }
    }

    public class custom_RichTextBox : RichTextBox
    {
        public custom_RichTextBox()
            : base()
        {

        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr LoadLibrary(string lpFileName);

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams param = base.CreateParams;
                if (LoadLibrary("msftedit.dll") != IntPtr.Zero)
                {
                    param.ClassName = "RICHEDIT50W";
                }
                return param;
            }
        }

        private enum EmfToWmfBitsFlags
        {
            EmfToWmfBitsFlagsDefault = 0x00000000,
            EmfToWmfBitsFlagsEmbedEmf = 0x00000001,
            EmfToWmfBitsFlagsIncludePlaceable = 0x00000002,
            EmfToWmfBitsFlagsNoXORClip = 0x00000004
        };

        private struct RtfFontFamilyDef
        {
            public const string Unknown = @"\fnil";
            public const string Roman = @"\froman";
            public const string Swiss = @"\fswiss";
            public const string Modern = @"\fmodern";
            public const string Script = @"\fscript";
            public const string Decor = @"\fdecor";
            public const string Technical = @"\ftech";
            public const string BiDirect = @"\fbidi";
        }

        [DllImport("gdiplus.dll")]
        private static extern uint GdipEmfToWmfBits(IntPtr _hEmf,
          uint _bufferSize, byte[] _buffer,
          int _mappingMode, EmfToWmfBitsFlags _flags);


        public static string GetImagePrefix(Image _image)
        {
            float xDpi, yDpi;
            StringBuilder rtf = new StringBuilder();
            using (Graphics graphics = Graphics.FromImage(_image))
            {
                xDpi = graphics.DpiX;
                yDpi = graphics.DpiY;
            };
            // Calculate the current width of the image in (0.01)mm
            int picw = (int)Math.Round((_image.Width / xDpi) * 2540);
            // Calculate the current height of the image in (0.01)mm
            int pich = (int)Math.Round((_image.Height / yDpi) * 2540);
            // Calculate the target width of the image in twips
            int picwgoal = (int)Math.Round((_image.Width / xDpi) * 1440);
            // Calculate the target height of the image in twips
            int pichgoal = (int)Math.Round((_image.Height / yDpi) * 1440);
            // Append values to RTF string
            rtf.Append(@"{\pict\wmetafile8");
            rtf.Append(@"\picw");
            rtf.Append(picw);
            rtf.Append(@"\pich");
            rtf.Append(pich);
            rtf.Append(@"\picwgoal");
            rtf.Append(picwgoal);
            rtf.Append(@"\pichgoal");
            rtf.Append(pichgoal);
            rtf.Append(" ");

            return rtf.ToString();
        }
    }
}
