﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;

namespace Spryt
{
    public class PaletteChangedEventArgs : EventArgs
    {
        public readonly Color[] Palette;

        public PaletteChangedEventArgs( Color[] palette )
        {
            Palette = palette;
        }
    }

    public class SelectedColourChangedEventArgs : EventArgs
    {
        public readonly int SelectedIndex;
        public readonly Color SelectedColour;

        public Pixel SelectedPixed
        {
            get { return (Pixel) ( 8 | SelectedIndex ); }
        }

        public SelectedColourChangedEventArgs( ColourPalettePanel panel )
        {
            SelectedIndex = panel.SelectedIndex;
            SelectedColour = panel.Palette[ panel.SelectedIndex ];
        }
    }

    public partial class ColourPalettePanel : UserControl
    {
        private static readonly int[] stDefaultPalette = new int[]
        {
            0x000000, 0x242424, 0x484848, 0x6d6d6d, 0x919191, 0xb6b6b6, 0xdadada, 0xffffff
        };

        private Button[] myColourBtns;
        private Button[] myPickerBtns;

        private int mySelectedIndex;

        public Color[] Palette { get; private set; }
        public int SelectedIndex
        {
            get { return mySelectedIndex; }
            set
            {
                value -= (int) Math.Floor( (float) value / 8 ) * 8;

                mySelectedIndex = value;
                selectedColourPanel.BackColor = Palette[ value ];

                for ( int i = 0; i < 8; ++i )
                {
                    if ( i == value )
                        myColourBtns[ i ].FlatAppearance.BorderSize = 3;
                    else
                        myColourBtns[ i ].FlatAppearance.BorderSize = 1;
                }

                if ( SelectedColourChanged != null )
                    SelectedColourChanged( this, new SelectedColourChangedEventArgs( this ) );
            }
        }

        public Color SelectedColour
        {
            get { return Palette[ mySelectedIndex ]; }
        }

        public event EventHandler<PaletteChangedEventArgs> PaletteChanged;
        public event EventHandler<SelectedColourChangedEventArgs> SelectedColourChanged;

        public ColourPalettePanel()
        {
            Palette = new Color[ 8 ];

            InitializeComponent();

            myColourBtns = new Button[ 8 ];
            myPickerBtns = new Button[ 8 ];
            for ( int i = 0; i < 8; ++i )
            {
                int index = i;

                Button clrBtn = myColourBtns[ i ] = new Button();
                tableLayoutPanel3.Controls.Add( clrBtn, 0, i );

                clrBtn.Dock = System.Windows.Forms.DockStyle.Fill;
                clrBtn.Name = "colour" + i;
                clrBtn.Size = new System.Drawing.Size( 100, 26 );
                clrBtn.TabIndex = 1;
                clrBtn.UseVisualStyleBackColor = false;
                clrBtn.FlatStyle = FlatStyle.Flat;
                clrBtn.FlatAppearance.BorderSize = 1;

                clrBtn.Click += ( sender, args ) =>
                {
                    SelectedIndex = index;
                };

                Button pickBtn = myPickerBtns[ i ] = new Button();
                tableLayoutPanel3.Controls.Add( pickBtn, 1, i );

                pickBtn.Dock = System.Windows.Forms.DockStyle.Fill;
                pickBtn.Image = global::Spryt.Properties.Resources.color_wheel;
                pickBtn.Name = "picker1";
                pickBtn.Size = new System.Drawing.Size( 26, 26 );
                pickBtn.TabIndex = 0;
                pickBtn.UseVisualStyleBackColor = true;

                pickBtn.Click += ( sender, args ) =>
                {
                    ColorDialog dialog = new ColorDialog();
                    dialog.Color = clrBtn.BackColor;
                    DialogResult res = dialog.ShowDialog();

                    if ( res == DialogResult.OK )
                    {
                        SetColour( index, dialog.Color );

                        if ( PaletteChanged != null )
                            PaletteChanged( this, new PaletteChangedEventArgs( Palette ) );
                    
                        if( SelectedIndex == index && SelectedColourChanged != null )
                            SelectedColourChanged( this, new SelectedColourChangedEventArgs( this ) );
                    }
                };

                int r = ( stDefaultPalette[ i ] >> 16 ) & 0xff;
                int g = ( stDefaultPalette[ i ] >> 08 ) & 0xff;
                int b = ( stDefaultPalette[ i ] >> 00 ) & 0xff;

                SetColour( i, Color.FromArgb( r, g, b ) );
            }

            SelectedIndex = 0;
        }

        public void SetPalette( Color[] palette )
        {
            for ( int i = 0; i < 8; ++i )
                SetColour( i, palette[ i ] );

            if ( PaletteChanged != null )
                PaletteChanged( this, new PaletteChangedEventArgs( Palette ) );
        }

        private void SetColour( int index, Color colour )
        {
            Palette[ index ] = colour;

            int r = ( colour.R + 128 ) % 256;
            int g = ( colour.G + 128 ) % 256;
            int b = ( colour.B + 128 ) % 256;

            myColourBtns[ index ].BackColor = colour;
            myColourBtns[ index ].ForeColor = Color.FromArgb( r, g, b );

            myColourBtns[ index ].Text = String.Format( "#{0:X2}{1:X2}{2:X2}", colour.R, colour.G, colour.B );

            if ( index == SelectedIndex )
                selectedColourPanel.BackColor = Palette[ index ];
        }

        private void presetComboBox_DropDown( object sender, EventArgs e )
        {
            presetComboBox.Items.Clear();

            if ( Directory.Exists( "palettes" ) )
                foreach ( String file in Directory.EnumerateFiles( "palettes/" ) )
                    if ( file.EndsWith( ".spf" ) )
                        presetComboBox.Items.Add( file.Substring( 9, file.Length - 13 ) );
        }

        private void saveBtn_Click( object sender, EventArgs e )
        {
            String filePath = "palettes/" + presetComboBox.Text + ".spf";

            if ( File.Exists( filePath ) && MessageBox.Show( "Overwrite existing palette?",
                "Save Palette", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation ) == DialogResult.No )
                return;

            using ( FileStream stream = new FileStream( filePath, FileMode.Create, FileAccess.Write ) )
            {
                using ( StreamWriter writer = new StreamWriter( stream ) )
                {
                    for ( int i = 0; i < 8; ++i )
                    {
                        Color colour = Palette[ i ];
                        writer.WriteLine( "#{0:X2}{1:X2}{2:X2}", colour.R, colour.G, colour.B );
                    }
                }
            }
        }

        private void loadBtn_Click( object sender, EventArgs e )
        {
            String filePath = "palettes/" + presetComboBox.Text + ".spf";

            if ( !File.Exists( filePath ) )
            {
                MessageBox.Show( "Palette file not found.", "Load Palette", MessageBoxButtons.OK, MessageBoxIcon.Error );
                return;
            }

            using ( FileStream stream = new FileStream( filePath, FileMode.Open, FileAccess.Read ) )
            {
                using ( StreamReader reader = new StreamReader( stream ) )
                {
                    for ( int i = 0; i < 8; ++i )
                    {
                        String line;
                        do
                        {
                            if ( reader.EndOfStream )
                            {
                                MessageBox.Show( "Palette file corrupted.", "Load Palette", MessageBoxButtons.OK, MessageBoxIcon.Error );
                                return;
                            }

                            line = reader.ReadLine();
                        }
                        while ( line.Length != 7 || !Regex.IsMatch( line, "#[0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F]" ) );

                        int r = int.Parse( line.Substring( 1, 2 ), System.Globalization.NumberStyles.HexNumber );
                        int g = int.Parse( line.Substring( 3, 2 ), System.Globalization.NumberStyles.HexNumber );
                        int b = int.Parse( line.Substring( 5, 2 ), System.Globalization.NumberStyles.HexNumber );

                        SetColour( i, Color.FromArgb( r, g, b ) );
                    }
                }
            }

            if ( PaletteChanged != null )
                PaletteChanged( this, new PaletteChangedEventArgs( Palette ) );
        }

        private void deleteBtn_Click( object sender, EventArgs e )
        {
            String filePath = "palettes/" + presetComboBox.Text + ".spf";

            if ( !File.Exists( filePath ) || MessageBox.Show( "Are you sure you want to delete this palette?",
                "Delete Palette", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation ) == DialogResult.No )
                return;

            File.Delete( filePath );

            presetComboBox.Text = null;
        }

        private void presetComboBox_TextUpdate( object sender, EventArgs e )
        {
            loadBtn.Enabled = saveBtn.Enabled = deleteBtn.Enabled = presetComboBox.Text != null && presetComboBox.Text.Length > 0;
        }
    }
}
