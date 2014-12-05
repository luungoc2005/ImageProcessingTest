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

                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        currentX += (p[0]+p[1]+p[2])/3;
                        if (y>0)
                        {
                            intergralImage[(int)x, y] = currentX + intergralImage[(int)x, y-1];
                        }
                        else
                        {
                            intergralImage[(int)x, y] = currentX;
                        }
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

                        if (((p[0]+p[1]+p[2])/3) >= threshold)
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

        public static Bitmap RotateImage(Image image, float angle)
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

            float[] sins = new float[180] { (float)0.0, (float)0.017452, (float)0.034899, (float)0.052336, (float)0.069756, (float)0.087156, (float)0.104528, (float)0.121869, (float)0.139173, (float)0.156434, (float)0.173648, (float)0.190809, (float)0.207912, (float)0.224951, (float)0.241922, (float)0.258819, (float)0.275637, (float)0.292372, (float)0.309017, (float)0.325568, (float)0.342020, (float)0.358368, (float)0.374607, (float)0.390731, (float)0.406737, (float)0.422618, (float)0.438371, (float)0.453990, (float)0.469472, (float)0.484810, (float)0.500000, (float)0.515038, (float)0.529919, (float)0.544639, (float)0.559193, (float)0.573576, (float)0.587785, (float)0.601815, (float)0.615662, (float)0.629320, (float)0.642788, (float)0.656059, (float)0.669131, (float)0.681998, (float)0.694658, (float)0.707107, (float)0.719340, (float)0.731354, (float)0.743145, (float)0.754710, (float)0.766044, (float)0.777146, (float)0.788011, (float)0.798636, (float)0.809017, (float)0.819152, (float)0.829038, (float)0.838671, (float)0.848048, (float)0.857167, (float)0.866025, (float)0.874620, (float)0.882948, (float)0.891007, (float)0.898794, (float)0.906308, (float)0.913545, (float)0.920505, (float)0.927184, (float)0.933580, (float)0.939693, (float)0.945519, (float)0.951057, (float)0.956305, (float)0.961262, (float)0.965926, (float)0.970296, (float)0.974370, (float)0.978148, (float)0.981627, (float)0.984808, (float)0.987688, (float)0.990268, (float)0.992546, (float)0.994522, (float)0.996195, (float)0.997564, (float)0.998630, (float)0.999391, (float)0.999848, (float)1.0, (float)0.999848, (float)0.999391, (float)0.998630, (float)0.997564, (float)0.996195, (float)0.994522, (float)0.992546, (float)0.990268, (float)0.987688, (float)0.984808, (float)0.981627, (float)0.978148, (float)0.974370, (float)0.970296, (float)0.965926, (float)0.961262, (float)0.956305, (float)0.951057, (float)0.945519, (float)0.939693, (float)0.933580, (float)0.927184, (float)0.920505, (float)0.913545, (float)0.906308, (float)0.898794, (float)0.891007, (float)0.882948, (float)0.874620, (float)0.866025, (float)0.857167, (float)0.848048, (float)0.838671, (float)0.829038, (float)0.819152, (float)0.809017, (float)0.798635, (float)0.788011, (float)0.777146, (float)0.766044, (float)0.754710, (float)0.743145, (float)0.731354, (float)0.719340, (float)0.707107, (float)0.694658, (float)0.681998, (float)0.669131, (float)0.656059, (float)0.642788, (float)0.629321, (float)0.615661, (float)0.601815, (float)0.587785, (float)0.573576, (float)0.559193, (float)0.544639, (float)0.529919, (float)0.515038, (float)0.500000, (float)0.484810, (float)0.469472, (float)0.453991, (float)0.438371, (float)0.422618, (float)0.406737, (float)0.390731, (float)0.374607, (float)0.358368, (float)0.342020, (float)0.325568, (float)0.309017, (float)0.292372, (float)0.275637, (float)0.258819, (float)0.241922, (float)0.224951, (float)0.207912, (float)0.190809, (float)0.173648, (float)0.156434, (float)0.139173, (float)0.121869, (float)0.104528, (float)0.087156, (float)0.069756, (float)0.052336, (float)0.034899, (float)0.017452 };
            float[] coss = new float[180] { (float)1.0, (float)0.999848, (float)0.999391, (float)0.998630, (float)0.997564, (float)0.996195, (float)0.994522, (float)0.992546, (float)0.990268, (float)0.987688, (float)0.984808, (float)0.981627, (float)0.978148, (float)0.974370, (float)0.970296, (float)0.965926, (float)0.961262, (float)0.956305, (float)0.951057, (float)0.945519, (float)0.939693, (float)0.933580, (float)0.927184, (float)0.920505, (float)0.913545, (float)0.906308, (float)0.898794, (float)0.891007, (float)0.882948, (float)0.874620, (float)0.866025, (float)0.857167, (float)0.848048, (float)0.838671, (float)0.829038, (float)0.819152, (float)0.809017, (float)0.798636, (float)0.788011, (float)0.777146, (float)0.766044, (float)0.754710, (float)0.743145, (float)0.731354, (float)0.719340, (float)0.707107, (float)0.694658, (float)0.681998, (float)0.669131, (float)0.656059, (float)0.642788, (float)0.629320, (float)0.615662, (float)0.601815, (float)0.587785, (float)0.573576, (float)0.559193, (float)0.544639, (float)0.529919, (float)0.515038, (float)0.500000, (float)0.484810, (float)0.469472, (float)0.453991, (float)0.438371, (float)0.422618, (float)0.406737, (float)0.390731, (float)0.374607, (float)0.358368, (float)0.342020, (float)0.325568, (float)0.309017, (float)0.292372, (float)0.275637, (float)0.258819, (float)0.241922, (float)0.224951, (float)0.207912, (float)0.190809, (float)0.173648, (float)0.156434, (float)0.139173, (float)0.121869, (float)0.104528, (float)0.087156, (float)0.069757, (float)0.052336, (float)0.034899, (float)0.017452, (float)0.0, (float)-0.017452, (float)-0.034899, (float)-0.052336, (float)-0.069756, (float)-0.087156, (float)-0.104529, (float)-0.121869, (float)-0.139173, (float)-0.156434, (float)-0.173648, (float)-0.190809, (float)-0.207912, (float)-0.224951, (float)-0.241922, (float)-0.258819, (float)-0.275637, (float)-0.292372, (float)-0.309017, (float)-0.325568, (float)-0.342020, (float)-0.358368, (float)-0.374607, (float)-0.390731, (float)-0.406737, (float)-0.422618, (float)-0.438371, (float)-0.453990, (float)-0.469472, (float)-0.484810, (float)-0.500000, (float)-0.515038, (float)-0.529919, (float)-0.544639, (float)-0.559193, (float)-0.573576, (float)-0.587785, (float)-0.601815, (float)-0.615661, (float)-0.629320, (float)-0.642788, (float)-0.656059, (float)-0.669131, (float)-0.681998, (float)-0.694658, (float)-0.707107, (float)-0.719340, (float)-0.731354, (float)-0.743145, (float)-0.754710, (float)-0.766044, (float)-0.777146, (float)-0.788011, (float)-0.798635, (float)-0.809017, (float)-0.819152, (float)-0.829037, (float)-0.838671, (float)-0.848048, (float)-0.857167, (float)-0.866025, (float)-0.874620, (float)-0.882948, (float)-0.891006, (float)-0.898794, (float)-0.906308, (float)-0.913545, (float)-0.920505, (float)-0.927184, (float)-0.933580, (float)-0.939693, (float)-0.945519, (float)-0.951056, (float)-0.956305, (float)-0.961262, (float)-0.965926, (float)-0.970296, (float)-0.974370, (float)-0.978148, (float)-0.981627, (float)-0.984808, (float)-0.987688, (float)-0.990268, (float)-0.992546, (float)-0.994522, (float)-0.996195, (float)-0.997564, (float)-0.998630, (float)-0.999391, (float)-0.999848 };

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
            distance = (int)Math.Sqrt(image.Width * image.Width + image.Height * image.Height);

            int[,] accumulator = new int[180, distance + 1];
            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    if (bwImage[x, y] == 0)
                    {
                        for (int i = 0; i < 180; i++)
                        {
                            //double rads = i*Math.PI/180.0;
                            //double d2 = Math.Max(0, y * Math.Sin(rads) + x * Math.Cos(rads));
                            double d2 = Math.Abs(y * sins[i] + x * coss[i]);
                            accumulator[i, (int)d2]++;
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

            for (int x = 0; x < image.Width; x++)
            {
                int y2 = (int)(((distance/2 - x) * Math.Cos((xMax) * Math.PI / 180.0)) / Math.Sin((xMax) * Math.PI / 180.0));
                image.SetPixel(x, Math.Max(0, Math.Min(y2, image.Height - 1)), Color.Red);
            }

            return xMax;
        }

    }
}
