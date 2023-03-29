#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

#endregion

namespace RenumberElementsByLine
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            // put any code needed for the form here

            // open form
            //MyForm currentForm = new MyForm()
            //{
            //    Width = 800,
            //    Height = 450,
            //    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
            //    Topmost = true,
            //};

            //currentForm.ShowDialog();

            Reference refLine = uidoc.Selection.PickObject(ObjectType.Element, "Select renumber line");

            Element curElem = doc.GetElement(refLine);

            if (curElem is ModelCurve || curElem is DetailCurve)
            {
                CurveElement curve = curElem as CurveElement;
                Curve curCurve = curve.GeometryCurve;

                if (curCurve is NurbSpline || curCurve is Arc)
                {
                    // note: this site might help - http://jeremytammik.github.io/tbc/a/1053_equi_distant_pts.htm
                    TaskDialog.Show("Error", "Cannot process arc or spline elements. Sorry. . . ");
                    return Result.Failed;
                }

                int precision = 2; // distance between check points in feet

                List<XYZ> checkPoints = PointSpacing(precision, curCurve);

                FilteredElementCollector collector = new FilteredElementCollector(doc);
                collector.OfCategory(BuiltInCategory.OST_Rooms);

                List<Room> roomList = new List<Room>();
                Dictionary<string, Room> roomDict = new Dictionary<string, Room>();

                foreach(XYZ checkPoint in checkPoints)
                {
                    foreach(Room curRoom in collector.ToList())
                    {
                        if(curRoom.IsPointInRoom(checkPoint))
                        {
                            if(roomDict.ContainsKey(curRoom.Number) == false)
                            {
                                Debug.Print(curRoom.Number);
                                roomDict.Add(curRoom.Number, curRoom);
                                break;
                            }
                        }
                    }
                }

                int counter = 1;
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Renumber rooms");
                    foreach (Room curRoom in roomDict.Values)
                    {
                        curRoom.Number = counter.ToString();
                        counter++;
                    }
                    t.Commit();
                }
            }
            else
            { 
                TaskDialog.Show("Error", "Please select a model or detail line.");
                return Result.Failed;
            }


            // get form data and do something

            return Result.Succeeded;
        }

        public static String GetMethod()
        {
            var method = MethodBase.GetCurrentMethod().DeclaringType?.FullName;
            return method;
        }

        private XYZ PointCalc(XYZ start, XYZ end, double distance)
        {
            // from: https://forums.autodesk.com/t5/revit-api-forum/points-along-a-curve/td-p/5719368

            // Origin point
            Double origin_x = start.X;
            Double origin_y = start.Y;

            // Point you are moving toward
            Double to_x = end.X;
            Double to_y = end.Y;

            Double fi = Math.Atan2(to_y - origin_y, to_x - origin_x);

            // Your final point
            Double final_x = origin_x + distance * Math.Cos(fi);
            Double final_y = origin_y + distance * Math.Sin(fi);

            XYZ xyz = new XYZ(final_x, final_y, end.Z);

            return xyz;

        }

        private List<XYZ> PointSpacing(double dist, Curve curve)
        {
            List<XYZ> pts = new List<XYZ>();

            double length = curve.Length;

            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);

            pts.Add(start);

            XYZ point = start;
            while (start.DistanceTo(point) < length)
            {
                point = PointCalc(point, end, dist);
                pts.Add(point);
            }
            return pts;

        }

    }
}
