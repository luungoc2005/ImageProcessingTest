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
                    for (int x = 0; x < image.Width*3; x+=3)
                    {
                        currentX += (p[0]+p[1]+p[2])/3;
                        if (y>0)
                        {
                            intergralImage[(int)x / 3, y] = currentX + intergralImage[(int)x / 3, y-1];
                        }
                        else
                        {
                            intergralImage[(int)x / 3, y] = currentX;
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
                    for (int x = 0; x < image.Width * 3; x+=3)
                    {
                        int x1 = Math.Max(0, x / 3 - res);
                        int y1 = Math.Max(0, y - res);
                        int x2 = Math.Min(bm.Width-1, x / 3 + res);
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

            int stride = bm.Stride;
            System.IntPtr scan0 = bm.Scan0;

            int[] angles=new int[89];

            unsafe
            {
            }

            int maxVotes = angles[0];
            for (int i = 0; i < 89; i++)
            {
                if (angles[i] > maxVotes) maxVotes = angles[i];
            }

            return maxVotes;
        }
    }
}
