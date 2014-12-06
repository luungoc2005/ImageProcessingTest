using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;

namespace ImagesAppTest
{
    abstract class ImageProcessor
    {
        public static void AdaptiveThreshold(Bitmap image)
        {            
            BitmapData bm = image.LockBits(new Rectangle(0, 0, image.Width, image.Height),
                System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            int[,] intergralImage = new int[image.Width, image.Height];
            int stride = bm.Stride;
            System.IntPtr scan0 = bm.Scan0;

            int res = 6; //resolution is 12x12
            int div = (res * 2) * (res * 2);

            unsafe
            {
                byte* p = (byte*)(void*)scan0;
                int offset = stride - image.Width * 3;

                //get the intergralImage
                int currentX=0;

                for (int x = 0; x < image.Width; x++)
                {
                    currentX += (p[0] + p[1] + p[2]) / 3;
                    intergralImage[x, 0] = currentX;
                    p += 3;
                }
                p += offset;
                currentX = 0;

                for (int y = 1; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        //currentX += (p[0] + p[1] + p[2]) / 3;
                        currentX += ((p[0] * 28 + p[1] * 77 + p[2] * 150) >> 8) & 255;
                        intergralImage[x, y] = currentX + intergralImage[x, y - 1];
                        p+=3; //Advance by 1b
                    }

                    currentX = 0;
                    p += offset;
                }

                //Thresholding using intergralImage

                p = (byte*)(void*)scan0;

                for (int y = 0; y < image.Height; y++)
                {                    
                    for (int x = 0; x < image.Width; x++)
                    {
                        int x1 = Math.Max(0, x - res);
                        int y1 = Math.Max(0, y - res);
                        int x2 = Math.Min(bm.Width-1, x + res);
                        int y2 = Math.Min(bm.Height-1, y + res);

                        int threshold = ((intergralImage[x2, y2] - intergralImage[x1, y2]
                                        -intergralImage[x2,y1]+intergralImage[x1,y1])) / div;

                        //if (((p[0]+p[1]+p[2])/3) >= threshold)
                        if ((((p[0] * 28 + p[1] * 77 + p[2] * 150) >> 8) & 255) >= threshold)
                        {
                            p[0] = 255;
                            p[1] = 255;
                            p[2] = 255;
                        }
                        else
                        {
                            p[0] = 0;
                            p[1] = 0;
                            p[2] = 0;
                        }
                        p+=3; //Advance by 3b
                    }
                    p += offset;
                }
            }

            image.UnlockBits(bm);
        }

        public static Bitmap RotateImage(Image image, double angle)
        {
            if (image == null)
                throw new ArgumentNullException("image");

            const double pi2 = Math.PI / 2.0;

            // Why can't C# allow these to be const, or at least readonly
            // *sigh*  I'm starting to talk like Christian Graus :omg:
            double oldWidth = (double)image.Width;
            double oldHeight = (double)image.Height;

            // Convert degrees to radians
            double theta = ((double)angle) * Math.PI / 180.0;
            double locked_theta = theta;

            // Ensure theta is now [0, 2pi)
            while (locked_theta < 0.0)
                locked_theta += 2 * Math.PI;

            double newWidth, newHeight;
            int nWidth, nHeight; // The newWidth/newHeight expressed as ints

            #region Explaination of the calculations
            /*
			 * The trig involved in calculating the new width and height
			 * is fairly simple; the hard part was remembering that when 
			 * PI/2 <= theta <= PI and 3PI/2 <= theta < 2PI the width and 
			 * height are switched.
			 * 
			 * When you rotate a rectangle, r, the bounding box surrounding r
			 * contains for right-triangles of empty space.  Each of the 
			 * triangles hypotenuse's are a known length, either the width or
			 * the height of r.  Because we know the length of the hypotenuse
			 * and we have a known angle of rotation, we can use the trig
			 * function identities to find the length of the other two sides.
			 * 
			 * sine = opposite/hypotenuse
			 * cosine = adjacent/hypotenuse
			 * 
			 * solving for the unknown we get
			 * 
			 * opposite = sine * hypotenuse
			 * adjacent = cosine * hypotenuse
			 * 
			 * Another interesting point about these triangles is that there
			 * are only two different triangles. The proof for which is easy
			 * to see, but its been too long since I've written a proof that
			 * I can't explain it well enough to want to publish it.  
			 * 
			 * Just trust me when I say the triangles formed by the lengths 
			 * width are always the same (for a given theta) and the same 
			 * goes for the height of r.
			 * 
			 * Rather than associate the opposite/adjacent sides with the
			 * width and height of the original bitmap, I'll associate them
			 * based on their position.
			 * 
			 * adjacent/oppositeTop will refer to the triangles making up the 
			 * upper right and lower left corners
			 * 
			 * adjacent/oppositeBottom will refer to the triangles making up 
			 * the upper left and lower right corners
			 * 
			 * The names are based on the right side corners, because thats 
			 * where I did my work on paper (the right side).
			 * 
			 * Now if you draw this out, you will see that the width of the 
			 * bounding box is calculated by adding together adjacentTop and 
			 * oppositeBottom while the height is calculate by adding 
			 * together adjacentBottom and oppositeTop.
			 */
            #endregion

            double adjacentTop, oppositeTop;
            double adjacentBottom, oppositeBottom;

            // We need to calculate the sides of the triangles based
            // on how much rotation is being done to the bitmap.
            //   Refer to the first paragraph in the explaination above for 
            //   reasons why.
            if ((locked_theta >= 0.0 && locked_theta < pi2) ||
                (locked_theta >= Math.PI && locked_theta < (Math.PI + pi2)))
            {
                adjacentTop = Math.Abs(Math.Cos(locked_theta)) * oldWidth;
                oppositeTop = Math.Abs(Math.Sin(locked_theta)) * oldWidth;

                adjacentBottom = Math.Abs(Math.Cos(locked_theta)) * oldHeight;
                oppositeBottom = Math.Abs(Math.Sin(locked_theta)) * oldHeight;
            }
            else
            {
                adjacentTop = Math.Abs(Math.Sin(locked_theta)) * oldHeight;
                oppositeTop = Math.Abs(Math.Cos(locked_theta)) * oldHeight;

                adjacentBottom = Math.Abs(Math.Sin(locked_theta)) * oldWidth;
                oppositeBottom = Math.Abs(Math.Cos(locked_theta)) * oldWidth;
            }

            newWidth = adjacentTop + oppositeBottom;
            newHeight = adjacentBottom + oppositeTop;

            nWidth = (int)Math.Ceiling(newWidth);
            nHeight = (int)Math.Ceiling(newHeight);

            Bitmap rotatedBmp = new Bitmap(nWidth, nHeight);

            using (Graphics g = Graphics.FromImage(rotatedBmp))
            {
                // This array will be used to pass in the three points that 
                // make up the rotated image
                Point[] points;

                /*
                 * The values of opposite/adjacentTop/Bottom are referring to 
                 * fixed locations instead of in relation to the
                 * rotating image so I need to change which values are used
                 * based on the how much the image is rotating.
                 * 
                 * For each point, one of the coordinates will always be 0, 
                 * nWidth, or nHeight.  This because the Bitmap we are drawing on
                 * is the bounding box for the rotated bitmap.  If both of the 
                 * corrdinates for any of the given points wasn't in the set above
                 * then the bitmap we are drawing on WOULDN'T be the bounding box
                 * as required.
                 */
                if (locked_theta >= 0.0 && locked_theta < pi2)
                {
                    points = new Point[] { 
											 new Point( (int) oppositeBottom, 0 ), 
											 new Point( nWidth, (int) oppositeTop ),
											 new Point( 0, (int) adjacentBottom )
										 };

                }
                else if (locked_theta >= pi2 && locked_theta < Math.PI)
                {
                    points = new Point[] { 
											 new Point( nWidth, (int) oppositeTop ),
											 new Point( (int) adjacentTop, nHeight ),
											 new Point( (int) oppositeBottom, 0 )						 
										 };
                }
                else if (locked_theta >= Math.PI && locked_theta < (Math.PI + pi2))
                {
                    points = new Point[] { 
											 new Point( (int) adjacentTop, nHeight ), 
											 new Point( 0, (int) adjacentBottom ),
											 new Point( nWidth, (int) oppositeTop )
										 };
                }
                else
                {
                    points = new Point[] { 
											 new Point( 0, (int) adjacentBottom ), 
											 new Point( (int) oppositeBottom, 0 ),
											 new Point( (int) adjacentTop, nHeight )		
										 };
                }

                g.DrawImage(image, points);
            }

            return rotatedBmp;
        }


        //This only works for threshold-filtered bitmaps
        public static int FindRotation(Bitmap image)
        {

            double[] sins = new double[180] { (double)0.0, (double)0.017452, (double)0.034899, (double)0.052336, (double)0.069756, (double)0.087156, (double)0.104528, (double)0.121869, (double)0.139173, (double)0.156434, (double)0.173648, (double)0.190809, (double)0.207912, (double)0.224951, (double)0.241922, (double)0.258819, (double)0.275637, (double)0.292372, (double)0.309017, (double)0.325568, (double)0.342020, (double)0.358368, (double)0.374607, (double)0.390731, (double)0.406737, (double)0.422618, (double)0.438371, (double)0.453990, (double)0.469472, (double)0.484810, (double)0.500000, (double)0.515038, (double)0.529919, (double)0.544639, (double)0.559193, (double)0.573576, (double)0.587785, (double)0.601815, (double)0.615662, (double)0.629320, (double)0.642788, (double)0.656059, (double)0.669131, (double)0.681998, (double)0.694658, (double)0.707107, (double)0.719340, (double)0.731354, (double)0.743145, (double)0.754710, (double)0.766044, (double)0.777146, (double)0.788011, (double)0.798636, (double)0.809017, (double)0.819152, (double)0.829038, (double)0.838671, (double)0.848048, (double)0.857167, (double)0.866025, (double)0.874620, (double)0.882948, (double)0.891007, (double)0.898794, (double)0.906308, (double)0.913545, (double)0.920505, (double)0.927184, (double)0.933580, (double)0.939693, (double)0.945519, (double)0.951057, (double)0.956305, (double)0.961262, (double)0.965926, (double)0.970296, (double)0.974370, (double)0.978148, (double)0.981627, (double)0.984808, (double)0.987688, (double)0.990268, (double)0.992546, (double)0.994522, (double)0.996195, (double)0.997564, (double)0.998630, (double)0.999391, (double)0.999848, (double)1.0, (double)0.999848, (double)0.999391, (double)0.998630, (double)0.997564, (double)0.996195, (double)0.994522, (double)0.992546, (double)0.990268, (double)0.987688, (double)0.984808, (double)0.981627, (double)0.978148, (double)0.974370, (double)0.970296, (double)0.965926, (double)0.961262, (double)0.956305, (double)0.951057, (double)0.945519, (double)0.939693, (double)0.933580, (double)0.927184, (double)0.920505, (double)0.913545, (double)0.906308, (double)0.898794, (double)0.891007, (double)0.882948, (double)0.874620, (double)0.866025, (double)0.857167, (double)0.848048, (double)0.838671, (double)0.829038, (double)0.819152, (double)0.809017, (double)0.798635, (double)0.788011, (double)0.777146, (double)0.766044, (double)0.754710, (double)0.743145, (double)0.731354, (double)0.719340, (double)0.707107, (double)0.694658, (double)0.681998, (double)0.669131, (double)0.656059, (double)0.642788, (double)0.629321, (double)0.615661, (double)0.601815, (double)0.587785, (double)0.573576, (double)0.559193, (double)0.544639, (double)0.529919, (double)0.515038, (double)0.500000, (double)0.484810, (double)0.469472, (double)0.453991, (double)0.438371, (double)0.422618, (double)0.406737, (double)0.390731, (double)0.374607, (double)0.358368, (double)0.342020, (double)0.325568, (double)0.309017, (double)0.292372, (double)0.275637, (double)0.258819, (double)0.241922, (double)0.224951, (double)0.207912, (double)0.190809, (double)0.173648, (double)0.156434, (double)0.139173, (double)0.121869, (double)0.104528, (double)0.087156, (double)0.069756, (double)0.052336, (double)0.034899, (double)0.017452 };
            double[] coss = new double[180] { (double)1.0, (double)0.999848, (double)0.999391, (double)0.998630, (double)0.997564, (double)0.996195, (double)0.994522, (double)0.992546, (double)0.990268, (double)0.987688, (double)0.984808, (double)0.981627, (double)0.978148, (double)0.974370, (double)0.970296, (double)0.965926, (double)0.961262, (double)0.956305, (double)0.951057, (double)0.945519, (double)0.939693, (double)0.933580, (double)0.927184, (double)0.920505, (double)0.913545, (double)0.906308, (double)0.898794, (double)0.891007, (double)0.882948, (double)0.874620, (double)0.866025, (double)0.857167, (double)0.848048, (double)0.838671, (double)0.829038, (double)0.819152, (double)0.809017, (double)0.798636, (double)0.788011, (double)0.777146, (double)0.766044, (double)0.754710, (double)0.743145, (double)0.731354, (double)0.719340, (double)0.707107, (double)0.694658, (double)0.681998, (double)0.669131, (double)0.656059, (double)0.642788, (double)0.629320, (double)0.615662, (double)0.601815, (double)0.587785, (double)0.573576, (double)0.559193, (double)0.544639, (double)0.529919, (double)0.515038, (double)0.500000, (double)0.484810, (double)0.469472, (double)0.453991, (double)0.438371, (double)0.422618, (double)0.406737, (double)0.390731, (double)0.374607, (double)0.358368, (double)0.342020, (double)0.325568, (double)0.309017, (double)0.292372, (double)0.275637, (double)0.258819, (double)0.241922, (double)0.224951, (double)0.207912, (double)0.190809, (double)0.173648, (double)0.156434, (double)0.139173, (double)0.121869, (double)0.104528, (double)0.087156, (double)0.069757, (double)0.052336, (double)0.034899, (double)0.017452, (double)0.0, (double)-0.017452, (double)-0.034899, (double)-0.052336, (double)-0.069756, (double)-0.087156, (double)-0.104529, (double)-0.121869, (double)-0.139173, (double)-0.156434, (double)-0.173648, (double)-0.190809, (double)-0.207912, (double)-0.224951, (double)-0.241922, (double)-0.258819, (double)-0.275637, (double)-0.292372, (double)-0.309017, (double)-0.325568, (double)-0.342020, (double)-0.358368, (double)-0.374607, (double)-0.390731, (double)-0.406737, (double)-0.422618, (double)-0.438371, (double)-0.453990, (double)-0.469472, (double)-0.484810, (double)-0.500000, (double)-0.515038, (double)-0.529919, (double)-0.544639, (double)-0.559193, (double)-0.573576, (double)-0.587785, (double)-0.601815, (double)-0.615661, (double)-0.629320, (double)-0.642788, (double)-0.656059, (double)-0.669131, (double)-0.681998, (double)-0.694658, (double)-0.707107, (double)-0.719340, (double)-0.731354, (double)-0.743145, (double)-0.754710, (double)-0.766044, (double)-0.777146, (double)-0.788011, (double)-0.798635, (double)-0.809017, (double)-0.819152, (double)-0.829037, (double)-0.838671, (double)-0.848048, (double)-0.857167, (double)-0.866025, (double)-0.874620, (double)-0.882948, (double)-0.891006, (double)-0.898794, (double)-0.906308, (double)-0.913545, (double)-0.920505, (double)-0.927184, (double)-0.933580, (double)-0.939693, (double)-0.945519, (double)-0.951056, (double)-0.956305, (double)-0.961262, (double)-0.965926, (double)-0.970296, (double)-0.974370, (double)-0.978148, (double)-0.981627, (double)-0.984808, (double)-0.987688, (double)-0.990268, (double)-0.992546, (double)-0.994522, (double)-0.996195, (double)-0.997564, (double)-0.998630, (double)-0.999391, (double)-0.999848 };

            BitmapData bm = image.LockBits(new Rectangle(0, 0, image.Width, image.Height),
                            System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            byte[,] bwImage = new byte[image.Width, image.Height];
            int stride = bm.Stride;
            System.IntPtr scan0 = bm.Scan0;

            //copy to a 2d array
            unsafe
            {
                byte* p = (byte*)(void*)scan0;
                int offset = stride - image.Width * 3;

                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        bwImage[x, y] = p[0];
                        //if (bwImage[x, y] != 255) MessageBox.Show(x + "," + y);
                        p += 3; //Advance by 3b
                    }
                    p += offset;
                }
            }
            image.UnlockBits(bm);

            //Hough transform
            int distance = (int)((image.Width+ image.Height)*Math.Sqrt(2));
            //distance = (int)Math.Sqrt(image.Width * image.Width + image.Height * image.Height);

            int[,] accumulator = new int[180, distance + 1];
            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    if (bwImage[x, y] == 0)
                    {
                        //to avoid repeated type casting
                        double x1 = x;
                        double y1 = y;
                        for (int i = 60; i < 120; i++)
                        {
                            //double rads = i*Math.PI/180.0;
                            //double d2 = Math.Max(0, y * Math.Sin(rads) + x * Math.Cos(rads));
                            //double d2 = Math.Abs(y1 * sins[i] + x1 * coss[i]);
                            accumulator[i, (int)Math.Abs(y1 * sins[i] + x1 * coss[i])] ++;
                        }
                    }
                }
            }

            int xMax = 0, yMax = 0, nMax = 0;
            for (int x = 0; x < 180; x++)
            {
                //int n=0;
                for (int y = 0; y < distance; y++)
                {
                    if (accumulator[x, y] > nMax)
                    {
                        xMax = x;
                        yMax = y;
                        nMax = accumulator[x, y];
                    }

                }
            }

            //Displaying where the strongest line is
            for (int x = 0; x < image.Width; x++)
            {
                int y2 = (int)(((double)(x) * Math.Cos((180-xMax) * Math.PI / 180.0) + yMax) / Math.Sin((180-xMax) * Math.PI / 180.0));
                image.SetPixel(x, Math.Max(0, Math.Min(y2, image.Height - 1)), Color.Red);
            }

            return xMax;
        }

    }
}
