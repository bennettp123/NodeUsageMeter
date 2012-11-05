using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.UI;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MetroUsageMeter
{
    class ImakeMaker
    {
        private long usage, quota;
        private long rollover, start;

        public ImakeMaker(long usage, long quota, DateTime rolloverDate, DateTime startDate)
        {
            this.usage = usage;
            this.quota = quota;
            this.rollover = rolloverDate.Ticks;
            this.start = startDate.Ticks;
        }

        public async Task<StorageFile> getSquareImage()
        {
            // default for unknown sizes
            int width = 120;
            int height = 120;

            switch (DisplayProperties.ResolutionScale)
            {
                case ResolutionScale.Scale100Percent:
                    width = 150;
                    height = 150;
                    break;
                case ResolutionScale.Scale140Percent:
                    width = 210;
                    height = 210;
                    break;
                case ResolutionScale.Scale180Percent:
                    width = 270;
                    height = 270;
                    break;
            }
            
            return await getImage(width, height, isSquare:true);
        }

        public async Task<StorageFile> getWideImage()
        {
            // default for unknown sizes
            int width = 248;
            int height = 120;

            switch (DisplayProperties.ResolutionScale)
            {
                case ResolutionScale.Scale100Percent:
                    width = 310;
                    height = 150;
                    break;
                case ResolutionScale.Scale140Percent:
                    width = 434;
                    height = 210;
                    break;
                case ResolutionScale.Scale180Percent:
                    width = 558;
                    height = 270;
                    break;
            }

            return await getImage(width, height, isSquare: false);
        }

        protected async Task<StorageFile> getImage(int width, int height, bool isSquare)
        {
            Task<bool> cleanupTask = this.cleanUpImages(isSquare);

            WriteableBitmap bitmap = BitmapFactory.New(width, height);
            bitmap.GetBitmapContext();


            Color bg = Colors.Transparent;
            Color bgFill = Colors.Black;
            Color fg = Colors.White;
            Color fgFill = Color.FromArgb(255,0xf5,0x94,0);

            bitmap.Clear(bg);

            // Graph outline
            int graphXPos = 20;
            int graphYPos = 45;
            int graphRPad = 20;
            int graphLength = width - graphXPos - graphRPad;
            int graphHeight = 35;
            bitmap.FillRectangle(graphXPos, graphYPos, graphXPos + graphLength, graphYPos + graphHeight-1, bgFill);
            bitmap.DrawRectangle(graphXPos, graphYPos, graphXPos + graphLength, graphYPos + graphHeight, fg);

            // Graph filler (shows the quota used)
            double fillScale = 1.0 - ((double)usage / (double)quota);
            if (fillScale < 0)
                fillScale = 0;
            if (fillScale > 1)
                fillScale = 1;
            int fillXPos = graphXPos + 4;
            int fillYPos = graphYPos + 4;
            int fillFullLength = graphLength - 8;
            int fillHeight = graphHeight - 9;
            int fillLength = (int)(fillFullLength * fillScale);
            bitmap.FillRectangle(fillXPos, fillYPos, fillXPos + fillLength, fillYPos + fillHeight, fgFill);

            // Time marker
            long period = rollover - start;
            long now = DateTime.Now.Ticks - start;
            double timeScale = 1.0 - ((double)now / (double)period);
            if (timeScale < 0)
                timeScale = 0;
            if (timeScale > 1)
                timeScale = 1;
            int timeMarkerWidth = 12;
            int timeMarkerHeight = 15;
            int timeMarkerPad = 7;
            int timeMarkerTopXPos = fillXPos + (int)(fillFullLength * timeScale);
            int timeMarkerTopYPos = graphYPos + graphHeight + timeMarkerPad;
            int timeMarkerBottomLeftXPos = (int)(timeMarkerTopXPos - (timeMarkerWidth / 2)); 
            int timeMarkerBottomRightXPos = (int)(timeMarkerTopXPos + (timeMarkerWidth / 2));
            int timeMarkerBottomYPos = timeMarkerTopYPos + timeMarkerHeight;
            bitmap.FillTriangle(timeMarkerTopXPos, timeMarkerTopYPos, timeMarkerBottomLeftXPos, timeMarkerBottomYPos, timeMarkerBottomRightXPos, timeMarkerBottomYPos, bgFill);
            bitmap.FillTriangle(timeMarkerTopXPos, timeMarkerTopYPos, timeMarkerBottomLeftXPos, timeMarkerBottomYPos, timeMarkerBottomRightXPos, timeMarkerBottomYPos, fgFill);
            bitmap.DrawTriangle(timeMarkerTopXPos, timeMarkerTopYPos, timeMarkerBottomLeftXPos, timeMarkerBottomYPos, timeMarkerBottomRightXPos, timeMarkerBottomYPos, fg);

            await cleanupTask;

            StorageFile file = await getSomeFile(isSquare:isSquare);
            IRandomAccessStream pngStream = await file.OpenAsync(FileAccessMode.ReadWrite);
            BitmapEncoder pngEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, pngStream);

            pngEncoder.SetPixelData(
                pixelFormat: BitmapPixelFormat.Rgba8,
                alphaMode: BitmapAlphaMode.Straight,
                width: (uint)bitmap.PixelWidth,
                height: (uint)bitmap.PixelHeight,
                dpiX: 96.0,
                dpiY: 96.0,
                pixels: bitmap.ToByteArray()
                );
            await pngEncoder.FlushAsync();
            await pngStream.FlushAsync();
            pngStream.Dispose();
            
            return file;
        }

        private async Task<StorageFile> getSomeFile(bool isSquare)
        {
            StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            StorageFolder imagesFolder = await localFolder.CreateFolderAsync("images", CreationCollisionOption.OpenIfExists);
            StorageFolder subFolder;
            if (isSquare)
            {
                subFolder = await imagesFolder.CreateFolderAsync("square", CreationCollisionOption.OpenIfExists);
            }
            else
            {
                subFolder = await imagesFolder.CreateFolderAsync("wide", CreationCollisionOption.OpenIfExists);
            }

            string permitted = "qwertyuiopasdfghjklzxcvbnm";
            Random r = new Random();
            string filename = "";
            for (int i=0;i<8;i++)
            {
                filename += permitted[r.Next(0,permitted.Length)];
            }
            
            return await subFolder.CreateFileAsync(filename + ".png", CreationCollisionOption.GenerateUniqueName);
        }

        protected async Task<byte[]> getPixelData(BitmapDecoder decoder)
        {
            PixelDataProvider pixelProvider = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Rgba8,
                BitmapAlphaMode.Straight,
                new BitmapTransform(),
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb
                );
            return pixelProvider.DetachPixelData();
        }

        private async Task<bool> cleanUpImages(bool isSquare)
        {
            try
            {
                StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                StorageFolder imagesFolder = await localFolder.GetFolderAsync("images");
                StorageFolder subFolder;
                if (isSquare)
                {
                    subFolder = await imagesFolder.GetFolderAsync("square");
                }
                else
                {
                    subFolder = await imagesFolder.GetFolderAsync("wide");
                }

                foreach (StorageFile file in await subFolder.GetFilesAsync())
                {
                    file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
            catch (FileNotFoundException e)
            {
                //Don't care.
            }
            return true;
        }
    }
}
