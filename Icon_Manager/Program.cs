using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;

namespace Icon_Manager
{
    class Program
    {
        static void Main(string[] args)
        {
            Image result;
            Image img = Image.FromFile("..\\..\\icon_original.png");
            Image squareImg = makeSquareImage(img);
            //Image img = Image.FromFile("..\\..\\original.png");
            squareImg.Save("..\\..\\..\\Shuttle_Xamarin\\Shuttle\\Shuttle.Droid\\Resources\\drawable\\icon.png");

            // Droid
            result = resizeImage(squareImg, 200, 200);
            result.Save("..\\..\\..\\Shuttle_Xamarin\\Shuttle\\Shuttle.Droid\\Resources\\drawable\\splashScreenImage.png");
            result.Save("..\\..\\..\\Shuttle_Xamarin\\Shuttle\\Shuttle.iOS\\Resources\\splashScreenImage.png");

            result = resizeImage(squareImg, 512, 512);
            result.Save("..\\..\\createdImages\\Droid_highResolutionSymbol.png"); //used in google play

            result = padImage(squareImg, 1024, 500);
            result.Save("..\\..\\createdImages\\Droid_functionalGraphic.png"); //used in google play


            // iOS
            // 76x76 icon
            result = resizeImage(squareImg, 76, 76);
            result.Save("..\\..\\..\\Shuttle_Xamarin\\Shuttle\\Shuttle.iOS\\Resources\\Icon-76.png");

            // 120x120 icon
            result = resizeImage(squareImg, 120, 120);
            result.Save("..\\..\\..\\Shuttle_Xamarin\\Shuttle\\Shuttle.iOS\\Resources\\Icon-60@2x.png");

            // 152x152 icon
            result = resizeImage(squareImg, 152, 152);
            result.Save("..\\..\\..\\Shuttle_Xamarin\\Shuttle\\Shuttle.iOS\\Resources\\Icon-76@2x.png"); // iPad @2x iOS 7 (see project properties)

            // 167x167 icon
            result = resizeImage(squareImg, 167, 167);
            result.Save("..\\..\\..\\Shuttle_Xamarin\\Shuttle\\Shuttle.iOS\\Resources\\Icon-83.5@2x.png");

            result = padImage(squareImg, 640, 1136);
            result.Save("..\\..\\..\\Shuttle_Xamarin\\Shuttle\\Shuttle.iOS\\Resources\\Default-568h@2x.png");//wird für den splash screen support vom iPhone 5 benötigt.

            result = resizeImage(squareImg, 1024, 1024);
            result.Save("..\\..\\createdImages\\iOS_App-Symbol.jpg", ImageFormat.Jpeg); //used in the apple store

        }

        private static Image makeSquareImage(Image img)
        {
            int newSideLength = Math.Max(img.Width, img.Height);

            Bitmap imgResized = new Bitmap(newSideLength, newSideLength, img.PixelFormat);
            imgResized.SetResolution(img.HorizontalResolution, img.VerticalResolution);

            Graphics imgResizedGraphics = Graphics.FromImage(imgResized);
            imgResizedGraphics.Clear(Color.Transparent);
            imgResizedGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            int upperLeft_X = (newSideLength - img.Width) / 2;
            int upperLeft_Y = (newSideLength - img.Height) / 2;
            imgResizedGraphics.DrawImage(img, new Point(upperLeft_X, upperLeft_Y));

            return imgResized;
        }

        private static Image resizeImage(Image img, int width, int height)
        {
            int newSideLength = Math.Max(img.Width, img.Height);

            Bitmap imgResized = new Bitmap(width, height, img.PixelFormat);
            imgResized.SetResolution(img.HorizontalResolution, img.VerticalResolution);

            Graphics imgResizedGraphics = Graphics.FromImage(imgResized);
            imgResizedGraphics.Clear(Color.Transparent);
            imgResizedGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            imgResizedGraphics.DrawImage(img, 0, 0, width, height);

            return imgResized;
        }

        private static Image padImage(Image img, int width, int height)
        {
            int squareLength = Math.Min(width, height);

            img = resizeImage(img, squareLength, squareLength);

            Bitmap imgPadded = new Bitmap(width, height, img.PixelFormat);
            imgPadded.SetResolution(img.HorizontalResolution, img.VerticalResolution);

            Graphics imgResizedGraphics = Graphics.FromImage(imgPadded);
            var color = (new Bitmap(img)).GetPixel(5, 5); //a bit inside the image as ther are interpolation artefacts a the border.
            imgResizedGraphics.Clear(color);
            imgResizedGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            int upperLeft_X = (width - squareLength) / 2;
            int upperLeft_Y = (height - squareLength) / 2;
            imgResizedGraphics.DrawImage(img, new Point(upperLeft_X, upperLeft_Y));

            //remove interpolation artefacts
            var penWidth = 5;
            imgResizedGraphics.DrawRectangle(new Pen(color, penWidth), upperLeft_X, upperLeft_Y, img.Width, img.Height);

            return imgPadded;
        }

    }
}
