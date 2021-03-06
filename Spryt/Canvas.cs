﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;

namespace Spryt
{
    partial class Canvas : Panel, IDisposable
    {
        private static readonly Brush stBackgroundBrush = new TextureBrush( Spryt.Properties.Resources.background, System.Drawing.Drawing2D.WrapMode.Tile );
        private static readonly Pen stBoxPreviewPen = new Pen( Color.FromArgb( 127, SystemColors.Highlight ) );
        private static readonly Brush stBoxPreviewBrush = new SolidBrush( Color.FromArgb( 63, SystemColors.Highlight ) );
        private static readonly Brush stSelectedAreaBrush = new TextureBrush( Spryt.Properties.Resources.shaded );

        private Control myOldParent;
        private EventHandler myOnParentResizeHandler;
        private EventHandler myOnParentMouseEnterHandler;
        private EventHandler myOnParentDisposedHandler;

        private int myCurrentLayerIndex;

        private Size myDisplaySize;
        private Rectangle[] myViewRectangles;
        private Pen myGridPen;
        private System.Drawing.Drawing2D.GraphicsPath myGrid;

        private Point myAnchorPos;

        private bool myMovingSelected;
        private bool myMovingLayer;
        private Layer myTempLayer;
        private RectangleF myMovedRect;
        private bool mySelectingPixels;
        private bool[ , ] mySelectedPixels;
        private int mySelectedArea;
        private Region mySelectedRegion;
        private Region myScaledRegion;

        private bool myDrawingPencil;
        private bool myDrawingBox;
        private Rectangle myBoxPreview;

        private ToolPanel myToolPanel;

        internal readonly ImageInfo Image;

        public int CurrentLayerIndex
        {
            get { return myCurrentLayerIndex; }
            set
            {
                myCurrentLayerIndex = value;
            }
        }
        public Layer CurrentLayer
        {
            get { return Image.Layers[ myCurrentLayerIndex ]; }
            set { CurrentLayerIndex = Image.Layers.IndexOf( value ); }
        }

        public event EventHandler ImageChanged;

        public Canvas( ImageInfo image, ToolPanel toolInfoPanel )
        {
            myOnParentResizeHandler = new EventHandler( OnParentResize );
            myOnParentMouseEnterHandler = new EventHandler( OnParentMouseEnter );
            myOnParentDisposedHandler = new EventHandler( OnParentDisposed );

            myCurrentLayerIndex = 0;

            myMovingSelected = false;
            mySelectingPixels = false;
            myDrawingPencil = false;
            myDrawingBox = false;

            Image = image;
            myToolPanel = toolInfoPanel;

            mySelectedPixels = new bool[ image.Width, image.Height ];
            mySelectedArea = 0;
            mySelectedRegion = new Region( Rectangle.Empty );
            myScaledRegion = new Region( Rectangle.Empty );

            Size = new Size( image.Width * 8, image.Height * 8 );
            BorderStyle = BorderStyle.FixedSingle;

            DoubleBuffered = true;

            InitializeComponent();
        }

        private void Centre()
        {
            if( Parent != null )
                Location = new Point( ( Parent.Width - Width ) / 2, ( Parent.Height - Height ) / 2 );
        }

        private void UpdateClientSize()
        {
            myDisplaySize = new Size( (int) Math.Round( Image.Width * Image.ZoomScale ),
                (int) Math.Round( Image.Height * Image.ZoomScale ) );
            ClientSize = new Size( myDisplaySize.Width * ( Image.TiledView ? 3 : 1 ),
                myDisplaySize.Height * ( Image.TiledView ? 3 : 1 ) );

            if ( Image.TiledView )
            {
                myViewRectangles = new Rectangle[ 9 ];
                for ( int x = 0; x < 3; ++x )
                {
                    for ( int y = 0; y < 3; ++y )
                    {
                        int i = x * 3 + y;
                        myViewRectangles[ i ] = new Rectangle(
                            ClientRectangle.X + x * myDisplaySize.Width,
                            ClientRectangle.Y + y * myDisplaySize.Height,
                            myDisplaySize.Width,
                            myDisplaySize.Height );
                    }
                }
            }
            else
            {
                myViewRectangles = new Rectangle[] { ClientRectangle };
            }

            Centre();
        }

        public void UpdateZoomScale()
        {
            UpdateClientSize();
            UpdateGrid();
            UpdateScaledRegion();
            Invalidate();
        }

        public void UpdateGrid()
        {
            myGridPen = new Pen( Image.GridColour )
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
            };

            myGrid = new System.Drawing.Drawing2D.GraphicsPath();

            int xDiff = (int) ( Image.GridWidth * Image.ZoomScale );
            int yDiff = (int) ( Image.GridHeight * Image.ZoomScale );

            if ( xDiff > 1 )
            {
                for ( int x = Image.GridHorizontalOffset; x <= Image.Width; x += Image.GridWidth )
                {
                    myGrid.StartFigure();
                    myGrid.AddLine( x * Image.ZoomScale, 0,
                        x * Image.ZoomScale, myDisplaySize.Height );
                }
            }

            if ( yDiff > 1 )
            {
                for ( int y = Image.GridVerticalOffset; y <= Image.Height; y += Image.GridHeight )
                {
                    myGrid.StartFigure();
                    myGrid.AddLine( 0, y * Image.ZoomScale,
                        myDisplaySize.Width, y * Image.ZoomScale );
                }
            }

            Invalidate();
        }

        public void UpdateTiledView()
        {
            UpdateClientSize();
            Invalidate();
        }

        private void OnParentResize( object sender, EventArgs e )
        {
            Centre();
        }

        protected override void OnResize( EventArgs eventargs )
        {
            base.OnResize( eventargs );

            Centre();
        }

        protected override void OnMouseEnter( EventArgs e )
        {
            Focus();
        }

        private void OnParentMouseEnter( object sender, EventArgs e )
        {
            Focus();
        }

        public void SendImageChange()
        {
            if ( CurrentLayerIndex >= Image.Layers.Count )
                CurrentLayerIndex = Image.Layers.Count - 1;

            Invalidate();

            Image.Modified = true;

            if ( ImageChanged != null )
                ImageChanged( this, new EventArgs() );
        }

        protected override void OnKeyDown( KeyEventArgs e )
        {
            switch ( e.KeyCode )
            {
                case Keys.Z:
                    if ( ModifierKeys.HasFlag( Keys.Control ) )
                        Image.ActionStack.Undo();
                    break;
                case Keys.Y:
                    if ( ModifierKeys.HasFlag( Keys.Control ) )
                        Image.ActionStack.Redo();
                    break;
                case Keys.Delete:
                    if ( mySelectedArea != 0 )
                    {
                        bool change = false;

                        for ( int x = 0; x < Image.Width; ++x )
                        {
                            for ( int y = 0; y < Image.Height; ++y )
                            {
                                if ( mySelectedPixels[ x, y ] && CurrentLayer.Pixels[ x, y ] != Pixel.Empty )
                                {
                                    CurrentLayer.SetPixel( x, y, Pixel.Empty );
                                    change = true;
                                }
                            }
                        }

                        if ( change )
                            SendImageChange();
                    }
                    break;
            }
        }

        protected override void OnMouseDown( MouseEventArgs e )
        {
            if ( e.Button == MouseButtons.Left || e.Button == MouseButtons.Right )
            {
                int x = (int) ( ( e.X - ClientRectangle.Left ) / Image.ZoomScale );
                int y = (int) ( ( e.Y - ClientRectangle.Top ) / Image.ZoomScale );

                if( !Image.InBounds( x, y ) )
                    return;

                switch ( myToolPanel.CurrentTool )
                {
                    case Tool.Select:
                        x = Wrap( x, Image.Width );
                        y = Wrap( y, Image.Height );
                        if ( e.Button == MouseButtons.Left )
                        {
                            if ( mySelectedPixels[ x, y ] || ( mySelectedArea == 0 && CurrentLayer.Pixels[ x, y ] != Pixel.Empty ) )
                            {
                                myMovingSelected = myMovingLayer = true;
                                myTempLayer = null;
                                myAnchorPos = new Point( x, y );
                            }
                        }
                        else if( mySelectedPixels[ x, y ] )
                        {
                            myMovingSelected = true;
                            myAnchorPos = new Point( x, y );
                        }
                        break;
                    case Tool.Wand:
                        WandSelect( x, y, e.Button == MouseButtons.Right );
                        break;
                    case Tool.Area:
                        mySelectingPixels = true;
                        myAnchorPos = new Point( x, y );
                        myBoxPreview = new Rectangle( x, y, 1, 1 );
                        break;
                    case Tool.Pencil:
                        myDrawingPencil = true;
                        myAnchorPos = new Point( x, y );

                        DrawPencil( x, y, e.Button == MouseButtons.Left ? Image.CurrentPixel : Pixel.Empty );
                        break;
                    case Tool.Fill:
                        Fill( x, y, e.Button == MouseButtons.Left ? Image.CurrentPixel : Pixel.Empty );
                        Image.PushState();
                        break;
                    case Tool.Box:
                        myDrawingBox = true;
                        myAnchorPos = new Point( x, y );
                        myBoxPreview = new Rectangle( x, y, 1, 1 );
                        break;
                }
            }
        }

        protected override void OnMouseMove( MouseEventArgs e )
        {
            int x = (int) ( ( e.X - ClientRectangle.Left ) / Image.ZoomScale );
            int y = (int) ( ( e.Y - ClientRectangle.Top ) / Image.ZoomScale );

            if ( myMovingSelected )
            {
                if ( myMovingLayer && myTempLayer == null && ( x != myAnchorPos.X || y != myAnchorPos.Y ) )
                {
                    myTempLayer = new Layer( Image );
                    bool noSelection = mySelectedArea == 0;
                    for ( int px = 0; px < Image.Width; ++px )
                    {
                        for ( int py = 0; py < Image.Height; ++py )
                        {
                            if ( noSelection || mySelectedPixels[ px, py ] )
                            {
                                PixelSelect( px, py, CurrentLayer.Pixels[ px, py ] == Pixel.Empty );
                                myTempLayer.SetPixel( px, py, CurrentLayer.Pixels[ px, py ] );

                                if( !ModifierKeys.HasFlag( Keys.Control ) )
                                    CurrentLayer.SetPixel( px, py, Pixel.Empty );
                            }
                        }
                    }
                }

                UpdateMovedRect( x, y );
            }

            if ( myDrawingPencil )
            {
                if ( MouseButtons.HasFlag( MouseButtons.Left ) )
                    DrawPencil( x, y, Image.CurrentPixel );
                else
                    DrawPencil( x, y, Pixel.Empty );
            }

            if ( mySelectingPixels || myDrawingBox )
                UpdateBoxPreview( x, y );
        }

        protected override void OnMouseUp( MouseEventArgs e )
        {
            if ( e.Button == MouseButtons.Left || e.Button == MouseButtons.Right )
            {
                int x = (int) ( ( e.X - ClientRectangle.Left ) / Image.ZoomScale );
                int y = (int) ( ( e.Y - ClientRectangle.Top ) / Image.ZoomScale );

                if ( myMovingSelected )
                {
                    if ( myTempLayer != null || !myMovingLayer )
                    {
                        bool[,] selected = (bool[,]) mySelectedPixels.Clone();

                        AllSelect( true );

                        int dx = x - myAnchorPos.X;
                        int dy = y - myAnchorPos.Y;

                        for ( int px = 0; px < Image.Width; ++px )
                        {
                            for ( int py = 0; py < Image.Height; ++py )
                            {
                                int tx = Wrap( px - dx, Image.Width );
                                int ty = Wrap( py - dy, Image.Height );

                                if ( Image.InBounds( tx, ty ) && selected[ tx, ty ] )
                                {
                                    PixelSelect( px, py );

                                    if( myMovingLayer )
                                        CurrentLayer.SetPixel( px, py, myTempLayer.Pixels[ tx, ty ] );
                                }
                            }
                        }
                    }

                    myTempLayer = null;
                    myMovingLayer = false;
                    myMovingSelected = false;

                    Image.PushState();

                    SendImageChange();
                }

                if ( myDrawingPencil )
                {
                    myDrawingPencil = false;
                    Image.PushState();
                }

                if ( myDrawingBox || mySelectingPixels )
                {
                    if ( mySelectingPixels )
                    {
                        AreaSelect( x, y, e.Button == MouseButtons.Right );

                        mySelectingPixels = false;
                    }

                    if ( myDrawingBox )
                    {
                        if ( e.Button == MouseButtons.Left )
                            DrawBox( x, y, Image.CurrentPixel );
                        else
                            DrawBox( x, y, Pixel.Empty );

                        myDrawingBox = false;
                        Image.PushState();
                    }
                }
            }
        }

        private void AllSelect( bool deselect = false )
        {
            for ( int x = 0; x < Image.Width; ++x )
                for ( int y = 0; y < Image.Height; ++y )
                    PixelSelect( x, y, deselect );
        }

        private void PixelSelect( int x, int y, bool deselect = false )
        {
            x = Wrap( x, Image.Width );
            y = Wrap( y, Image.Height );

            if ( mySelectedPixels[ x, y ] == deselect )
                mySelectedArea += ( deselect ? -1 : 1 );

            mySelectedPixels[ x, y ] = !deselect;

            if ( deselect )
                mySelectedRegion.Exclude( new Rectangle( x, y, 1, 1 ) );
            else
                mySelectedRegion.Union( new Rectangle( x, y, 1, 1 ) );
        }

        private void WandSelect( int x, int y, bool deselect = false )
        {
            x = Wrap( x, Image.Width );
            y = Wrap( y, Image.Height );

            Stack<Point> stack = new Stack<Point>();
            stack.Push( new Point( x, y ) );

            Pixel match = CurrentLayer.Pixels[ x, y ];

            while ( stack.Count > 0 )
            {
                Point pos = stack.Pop();
                if ( Image.TiledView )
                {
                    pos.X = Wrap( pos.X, Image.Width );
                    pos.Y = Wrap( pos.Y, Image.Height );
                }
                if ( CanDraw( pos.X, pos.Y, true ) && mySelectedPixels[ pos.X, pos.Y ] == deselect && CurrentLayer.Pixels[ pos.X, pos.Y ] == match )
                {
                    PixelSelect( pos.X, pos.Y, deselect );

                    stack.Push( new Point( pos.X - 1, pos.Y ) );
                    stack.Push( new Point( pos.X + 1, pos.Y ) );
                    stack.Push( new Point( pos.X, pos.Y - 1 ) );
                    stack.Push( new Point( pos.X, pos.Y + 1 ) );
                }
            }

            UpdateScaledRegion();
            Invalidate();
        }

        private void AreaSelect( int x, int y, bool deselect = false )
        {
            int maxWid = Image.Width * ( Image.TiledView ? 3 : 1 );
            int maxHei = Image.Height * ( Image.TiledView ? 3 : 1 );

            int left = Math.Max( Math.Min( x, myAnchorPos.X ), 0 );
            int right = Math.Min( Math.Max( x, myAnchorPos.X ), maxWid - 1 );
            int top = Math.Max( Math.Min( y, myAnchorPos.Y ), 0 );
            int bottom = Math.Min( Math.Max( y, myAnchorPos.Y ), maxHei - 1 );

            for ( int px = left; px <= right; ++px )
                for ( int py = top; py <= bottom; ++py )
                    PixelSelect( px, py, deselect );

            UpdateScaledRegion();
            Invalidate();
        }

        private void UpdateMovedRect( int x, int y )
        {
            int dx = x - myAnchorPos.X;
            int dy = y - myAnchorPos.Y;

            myMovedRect = new RectangleF( dx * Image.ZoomScale,
                dy * Image.ZoomScale,
                myDisplaySize.Width, myDisplaySize.Height );

            UpdateScaledRegion();
            myScaledRegion.Translate( dx * Image.ZoomScale, dy * Image.ZoomScale );

            Invalidate();
        }

        private void UpdateScaledRegion()
        {
            myScaledRegion = mySelectedRegion.Clone();
            myScaledRegion.Transform( new System.Drawing.Drawing2D.Matrix( Image.ZoomScale, 0.0f, 0.0f, Image.ZoomScale, 0.0f, 0.0f ) );
        }

        private bool CanDraw( int x, int y, bool ignoreSelected = false )
        {
            if ( Image.TiledView )
            {
                x = Wrap( x, Image.Width );
                y = Wrap( y, Image.Height );
            }

            return Image.InBounds( x, y ) && ( ignoreSelected || mySelectedArea == 0 || mySelectedPixels[ x, y ] );
        }

        private void DrawPencil( int x, int y, Pixel pixel )
        {
            LineRasterEnumerator line = new LineRasterEnumerator( myAnchorPos, new Point( x, y ) );
            while ( line.MoveNext() )
            {
                int lx = line.Current.X;
                int ly = line.Current.Y;

                if ( CanDraw( lx, ly ) )
                    CurrentLayer.SetPixel( lx, ly, pixel );
            }

            myAnchorPos = new Point( x, y );

            SendImageChange();
        }

        private int Wrap( int a, int b )
        {
            return a - (int) Math.Floor( (float) a / b ) * b;
        }

        private void Fill( int x, int y, Pixel pixel )
        {
            x = Wrap( x, Image.Width );
            y = Wrap( y, Image.Height );

            if ( !CanDraw( x, y ) )
                return;

            Stack<Point> stack = new Stack<Point>();
            stack.Push( new Point( x, y ) );

            Pixel match = CurrentLayer.Pixels[ x, y ];

            if ( match == pixel )
                return;

            while ( stack.Count > 0 )
            {
                Point pos = stack.Pop();
                if ( Image.TiledView )
                {
                    pos.X = Wrap( pos.X, Image.Width );
                    pos.Y = Wrap( pos.Y, Image.Height );
                }
                if ( CanDraw( pos.X, pos.Y ) && CurrentLayer.Pixels[ pos.X, pos.Y ] == match )
                {
                    CurrentLayer.SetPixel( pos.X, pos.Y, pixel );

                    stack.Push( new Point( pos.X - 1, pos.Y ) );
                    stack.Push( new Point( pos.X + 1, pos.Y ) );
                    stack.Push( new Point( pos.X, pos.Y - 1 ) );
                    stack.Push( new Point( pos.X, pos.Y + 1 ) );
                }
            }

            SendImageChange();
        }

        private void DrawBox( int x, int y, Pixel pixel )
        {
            int maxWid = Image.Width * ( Image.TiledView ? 3 : 1 );
            int maxHei = Image.Height * ( Image.TiledView ? 3 : 1 );

            int left = Math.Max( Math.Min( x, myAnchorPos.X ), 0 );
            int right = Math.Min( Math.Max( x, myAnchorPos.X ), maxWid - 1 );
            int top = Math.Max( Math.Min( y, myAnchorPos.Y ), 0 );
            int bottom = Math.Min( Math.Max( y, myAnchorPos.Y ), maxHei - 1 );
            
            for ( int px = left; px <= right; ++px )
                for ( int py = top; py <= bottom; ++py )
                    if ( CanDraw( px, py ) )
                        CurrentLayer.SetPixel( px, py, pixel );

            SendImageChange();
        }

        private void UpdateBoxPreview( int x, int y )
        {
            int maxWid = Image.Width * ( Image.TiledView ? 3 : 1 );
            int maxHei = Image.Height * ( Image.TiledView ? 3 : 1 );

            int left = Math.Max( Math.Min( x, myAnchorPos.X ), 0 );
            int right = Math.Min( Math.Max( x + 1, myAnchorPos.X + 1 ), maxWid );
            int top = Math.Max( Math.Min( y, myAnchorPos.Y ), 0 );
            int bottom = Math.Min( Math.Max( y + 1, myAnchorPos.Y + 1 ), maxHei );

            myBoxPreview = new Rectangle( (int) Math.Round( left * Image.ZoomScale ),
                (int) Math.Round( top * Image.ZoomScale ),
                (int) Math.Round( ( right - left ) * Image.ZoomScale ),
                (int) Math.Round( ( bottom - top ) * Image.ZoomScale ) );

            Invalidate();
        }

        protected override void OnParentChanged( EventArgs e )
        {
            base.OnParentChanged( e );

            if ( myOldParent != null )
            {
                myOldParent.Resize -= myOnParentResizeHandler;
                myOldParent.MouseEnter -= myOnParentMouseEnterHandler;
                myOldParent.Disposed -= myOnParentDisposedHandler;
            }

            if ( Parent != null )
            {
                Parent.Resize += myOnParentResizeHandler;
                Parent.MouseEnter += myOnParentMouseEnterHandler;
                Parent.Disposed += myOnParentDisposedHandler;

                Centre();
            }
            
            myOldParent = Parent;
        }

        protected override void OnPaint( PaintEventArgs e )
        {
            base.OnPaint( e );

            Rectangle bounds = new Rectangle( 0, 0, myDisplaySize.Width, myDisplaySize.Height );
            RectangleF srcRect = new RectangleF( -0.5f, -0.5f, Image.Width, Image.Height );

            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

            foreach ( Rectangle rect in myViewRectangles )
            {
                e.Graphics.TranslateTransform( rect.X, rect.Y );

                e.Graphics.FillRectangle( stBackgroundBrush, bounds );

                RectangleF destRect = new RectangleF( bounds.X, bounds.Y, bounds.Width, bounds.Height );

                foreach ( Layer layer in Image.Layers )
                    e.Graphics.DrawImage( layer.Bitmap, destRect, srcRect, GraphicsUnit.Pixel );

                e.Graphics.FillRegion( stSelectedAreaBrush, myScaledRegion );
                
                if ( Image.ShowGrid )
                    e.Graphics.DrawPath( myGridPen, myGrid );

                e.Graphics.TranslateTransform( -rect.X, -rect.Y );
            }

            if ( myMovingLayer && myTempLayer != null )
                e.Graphics.DrawImage( myTempLayer.Bitmap, myMovedRect, srcRect, GraphicsUnit.Pixel );

            if ( mySelectingPixels || myDrawingBox )
            {
                e.Graphics.FillRectangle( stBoxPreviewBrush, myBoxPreview );
                e.Graphics.DrawRectangle( stBoxPreviewPen, myBoxPreview );
            }
        }

        private void OnParentDisposed( object sender, EventArgs e )
        {
            Dispose();
        }

        public new void Dispose()
        {
            Parent = null;
            OnParentChanged( null );

            base.Dispose();
        }
    }
}
