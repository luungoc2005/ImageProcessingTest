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

        //This only works for threshold-filtered bitmaps
        public static int FindRotation(Bitmap image)
        {
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
                        p += 3; //Advance by 3b
                    }
                    p += offset;
                }
            }
            image.UnlockBits(bm);

            //Hough transform
            int[] angles=new int[89];
            int angleOffset=(180-angles.Length)/2;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x ++)
                {
                    if (bwImage[x, y] == 255)
                    {
                        for (int angle = 0; angle < angles.Length-1; angle++)
                        {
                            double rads = (double)(angle+angleOffset) * Math.PI / 180.0;
                            //try each angle
                            for (int x2 = x; x2 < image.Width; x2++)
                            {
                                int y2 = Math.Max(0,(Math.Sin(rads)==0)? y:
                                    Math.Min((int)((x * Math.Cos(rads) + (x2 - x)) / Math.Sin(rads)),image.Height-1));
                                if (bwImage[x2, y2] == 255) angles[angle] += 1; //+1 vote
                            }
                        }
                    }
                }
            }

            int maxVotes = 0;
            for (int i = 0; i < 89; i++)
            {
                if (angles[i] > angles[maxVotes]) maxVotes = i;
            }

            return maxVotes;
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
    }
}
