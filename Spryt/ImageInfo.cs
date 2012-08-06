﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

namespace Spryt
{
    class LayerRemovedEventArgs : EventArgs
    {
        public readonly int Index;

        public LayerRemovedEventArgs( int index )
        {
            Index = index;
        }
    }

    class ImageInfo
    {
        private string myFileName;
        private Color[] myPalette;
        private float myZoomScale;

        public String FileName
        {
            get
            {
                return myFileName;
            }
            set
            {
                myFileName = value;

                if ( Tab != null )
                    Tab.Text = myFileName;
            }
        }

        public TabPage Tab { get; private set; }
        public Canvas Canvas { get; private set; }

        public Size Size { get; private set; }
        public int Width
        {
            get { return Size.Width; }
        }
        public int Height
        {
            get { return Size.Height; }
        }

        public Color[] Palette
        {
            get { return myPalette; }
            set
            {
                myPalette = value;

                foreach ( Layer layer in Layers )
                    layer.UpdateBitmap();

                Canvas.Invalidate();
            }
        }

        public int ColourIndex { get; set; }
        public Pixel CurrentPixel
        {
            get { return (Pixel) ( 8 | ColourIndex ); }
        }

        public float ZoomScale
        {
            get { return myZoomScale; }
            set
            {
                myZoomScale = value;
                Canvas.UpdateZoomScale();
            }
        }

        public List<Layer> Layers { get; set; }

        public event EventHandler LayersChanged;

        public ImageInfo( ToolPanel toolInfoPanel, int width = 16, int height = 16, String name = "untitled" )
        {
            Size = new Size( width, height );
            FileName = name;

            Tab = new TabPage( name );
            Tab.ImageIndex = 0;
            Tab.BackColor = SystemColors.ControlDark;

            Canvas = new Canvas( this, toolInfoPanel );
            Canvas.Name = "canvas";
            Tab.Controls.Add( Canvas );

            Layers = new List<Layer>();
            Layers.Add( new Layer( this ) );
        }

        public bool InBounds( int x, int y )
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public void AddLayer( int index = -1, string label = null )
        {
            Layers.Insert( index, new Layer( this, label ) );

            if( LayersChanged != null )
                LayersChanged( this, new EventArgs() );
        }

        public void RemoveLayer( int index )
        {
            Layers.RemoveAt( index );

            if ( Layers.Count == 0 )
                Layers.Add( new Layer( this ) );

            if ( LayersChanged != null )
                LayersChanged( this, new EventArgs() );

            Canvas.SendImageChange();
        }

        public void SwapLayers( int indexA, int indexB )
        {
            Layer a = Layers[ indexA ];
            Layer b = Layers[ indexB ];

            Layers.RemoveAt( indexA );
            Layers.Insert( indexA, b );

            Layers.RemoveAt( indexB );
            Layers.Insert( indexB, a );

            if ( LayersChanged != null )
                LayersChanged( this, new EventArgs() );

            Canvas.SendImageChange();
        }
    }
}
