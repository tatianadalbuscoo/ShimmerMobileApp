/* 
 * macOS entry point: launches UIApplication with AppDelegate.
 */


using ObjCRuntime;
using UIKit;


namespace ShimmerInterface
{
    public class Program
    {

        // This is the main entry point of the application.
        static void Main(string[] args)
        {
            UIApplication.Main(args, null, typeof(AppDelegate));
        }
    }
}
