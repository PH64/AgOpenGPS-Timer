﻿using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public class CBoundaryLines
    {
        //list of coordinates of boundary line
        public List<vec3> bndLine = new List<vec3>();

        //the list of constants and multiples of the boundary
        public List<vec2> calcList = new List<vec2>();


        // the [point reduced version of boundary
        //the list of constants and multiples of the boundary
        public List<vec2> calcListEar = new List<vec2>();

        public List<vec2> bndLineEar = new List<vec2>();


        //area variable
        public double area;

        //boundary variables
        public bool isSet, isDriveAround, isDriveThru;

        //constructor
        public CBoundaryLines()
        {
            area = 0;
            isSet = false;
            isDriveAround = false;
            isDriveThru = false;
            bndLine.Capacity = 128;
            calcList.Capacity = 128;
            calcListEar.Capacity = 128;
            bndLineEar.Capacity = 128;
        }

        public void CalculateBoundaryHeadings()
        {
            //to calc heading based on next and previous points to give an average heading.
            int cnt = bndLine.Count;
            vec3[] arr = new vec3[cnt];
            cnt--;
            bndLine.CopyTo(arr);
            bndLine.Clear();

            //first point needs last, first, second points
            double heading = Math.Atan2(arr[1].easting - arr[cnt].easting, arr[1].northing - arr[cnt].northing);
            if (heading < 0) heading += glm.twoPI;
            bndLine.Add(new vec3(arr[0].easting, arr[0].northing, heading));

            //middle points
            for (int i = 1; i < cnt; i++)
            {
                heading = Math.Atan2(arr[i + 1].easting - arr[i - 1].easting, arr[i + 1].northing - arr[i - 1].northing);
                if (heading < 0) heading += glm.twoPI;
                bndLine.Add(new vec3(arr[i].easting, arr[i].northing, heading));
            }

            //last and first point
            heading = Math.Atan2(arr[0].easting - arr[cnt - 1].easting, arr[0].northing - arr[cnt - 1].northing);
            if (heading < 0) heading += glm.twoPI;
            bndLine.Add(new vec3(arr[0].easting, arr[0].northing, heading));
        }

        public void FixBoundaryLine(int bndNum)
        {
            double spacing;
            //boundary point spacing based on eq width
            //close if less then 30 ha, 60ha, more then 60
            if (area < 200000) spacing = 1.1;
            else if (area < 400000) spacing = 2.2;
            else spacing = 3.3;

            if (bndNum > 0) spacing *= 0.5;

            double distance;

            //make sure distance isn't too small between points on headland
            spacing *= 1.2;
            int bndCount = bndLine.Count;
            for (int i = 0; i < bndCount - 1; i++)
            {
                distance = glm.Distance(bndLine[i], bndLine[i + 1]);
                if (distance < spacing)
                {
                    bndLine.RemoveAt(i + 1);
                    bndCount = bndLine.Count;
                    i--;
                }
            }

            //make sure headings are correct for calculated points
            CalculateBoundaryHeadings();

            double delta = 0;
            bndLineEar?.Clear();

            for (int i = 0; i < bndLine.Count; i++)
            {
                if (i == 0)
                {
                    bndLineEar.Add(new vec2(bndLine[i].easting, bndLine[i].northing));
                    continue;
                }
                delta += (bndLine[i - 1].heading - bndLine[i].heading);
                if (Math.Abs(delta) > 0.01)
                {
                    bndLineEar.Add(new vec2(bndLine[i].easting, bndLine[i].northing));
                    delta = 0;
                }
            }
        }

        public void ReverseWinding()
        {
            //reverse the boundary
            int cnt = bndLine.Count;
            vec3[] arr = new vec3[cnt];
            cnt--;
            bndLine.CopyTo(arr);
            bndLine.Clear();
            for (int i = cnt; i >= 0; i--)
            {
                arr[i].heading -= Math.PI;
                if (arr[i].heading < 0) arr[i].heading += glm.twoPI;
                bndLine.Add(arr[i]);
            }
        }

        public void PreCalcBoundaryLines()
        {
            int j = bndLine.Count - 1;
            //clear the list, constant is easting, multiple is northing
            calcList.Clear();

            for (int i = 0; i < bndLine.Count; j = i++)
            {
                //check for divide by zero
                if (Math.Abs(bndLine[i].northing - bndLine[j].northing) < 0.00000000001)
                {
                    calcList.Add(new vec2(bndLine[i].easting, 0));
                }
                else
                {
                    //determine constant and multiple and add to list
                    calcList.Add(new vec2(
                    bndLine[i].easting - ((bndLine[i].northing * bndLine[j].easting)
                                    / (bndLine[j].northing - bndLine[i].northing)) + ((bndLine[i].northing * bndLine[i].easting)
                                        / (bndLine[j].northing - bndLine[i].northing)),
                    (bndLine[j].easting - bndLine[i].easting) / (bndLine[j].northing - bndLine[i].northing)));
                }
            }
        }

        public void PreCalcBoundaryEarLines()
        {
            int j = bndLineEar.Count - 1;
            //clear the list, constant is easting, multiple is northing
            calcListEar.Clear();

            for (int i = 0; i < bndLineEar.Count; j = i++)
            {
                //check for divide by zero
                if (Math.Abs(bndLineEar[i].northing - bndLineEar[j].northing) < 0.00000000001)
                {
                    calcListEar.Add(new vec2(bndLineEar[i].easting, 0));
                }
                else
                {
                    calcListEar.Add(new vec2(
                    //determine constant and multiple and add to list
                    bndLineEar[i].easting - ((bndLineEar[i].northing * bndLineEar[j].easting)
                                    / (bndLineEar[j].northing - bndLineEar[i].northing)) + ((bndLineEar[i].northing * bndLineEar[i].easting)
                                        / (bndLineEar[j].northing - bndLineEar[i].northing)),
                    (bndLineEar[j].easting - bndLineEar[i].easting) / (bndLineEar[j].northing - bndLineEar[i].northing)));
                }
            }
        }

        public bool IsPointInsideBoundary(vec3 testPointv3)
        {
            if (calcList.Count < 3) return false;
            int j = bndLine.Count - 1;
            bool oddNodes = false;

            //test against the constant and multiples list the test point
            for (int i = 0; i < bndLine.Count; j = i++)
            {
                if ((bndLine[i].northing < testPointv3.northing && bndLine[j].northing >= testPointv3.northing)
                || (bndLine[j].northing < testPointv3.northing && bndLine[i].northing >= testPointv3.northing))
                {
                    oddNodes ^= ((testPointv3.northing * calcList[i].northing) + calcList[i].easting < testPointv3.easting);
                }
            }
            return oddNodes; //true means inside.
        }

        public bool IsPointInsideBoundaryEar(vec3 testPointv3)
        {
            if (calcListEar.Count < 3) return false;
            int j = bndLineEar.Count - 1;
            bool oddNodes = false;

            //test against the constant and multiples list the test point
            for (int i = 0; i < bndLineEar.Count; j = i++)
            {
                if ((bndLineEar[i].northing < testPointv3.northing && bndLineEar[j].northing >= testPointv3.northing)
                || (bndLineEar[j].northing < testPointv3.northing && bndLineEar[i].northing >= testPointv3.northing))
                {
                    oddNodes ^= ((testPointv3.northing * calcListEar[i].northing) + calcListEar[i].easting < testPointv3.easting);
                }
            }
            return oddNodes; //true means inside.
        }

        public bool IsPointInsideBoundaryEar(vec2 testPointv2)
        {
            if (calcListEar.Count < 3) return false;
            int j = bndLineEar.Count - 1;
            bool oddNodes = false;

            //test against the constant and multiples list the test point
            for (int i = 0; i < bndLineEar.Count; j = i++)
            {
                if ((bndLineEar[i].northing < testPointv2.northing && bndLineEar[j].northing >= testPointv2.northing)
                || (bndLineEar[j].northing < testPointv2.northing && bndLineEar[i].northing >= testPointv2.northing))
                {
                    oddNodes ^= ((testPointv2.northing * calcListEar[i].northing) + calcListEar[i].easting < testPointv2.easting);
                }
            }
            return oddNodes; //true means inside.
        }

        public void DrawBoundaryLine(int lw, bool outOfBounds)
        {
            ////draw the perimeter line so far
            if (bndLine.Count < 1) return;
            //GL.PointSize(8);
            //int ptCount = bndLine.Count;
            //GL.Color3(0.925f, 0.752f, 0.860f);
            ////else 
            //GL.Begin(PrimitiveType.Points);
            //for (int h = 0; h < ptCount; h++) GL.Vertex3(bndLine[h].easting, bndLine[h].northing, 0);
            ////GL.Color3(0.95f, 0.972f, 0.90f);
            ////GL.Vertex3(bndLine[0].easting, bndLine[0].northing, 0);
            //GL.End();

            //ptCount = bdList.Count;
            //if (ptCount < 1) return;
            if (!outOfBounds)
            {
                GL.Color3(0.95f, 0.75f, 0.50f);
                GL.LineWidth(lw);
            }
            else
            {
                GL.LineWidth(lw * 3);
                GL.Color3(0.95f, 0.25f, 0.250f);
            }

            GL.Begin(PrimitiveType.LineLoop);
            for (int i = 0; i < bndLineEar.Count; i++)
            {
                GL.Vertex3(bndLineEar[i].easting, bndLineEar[i].northing, 0);
            }
            GL.End();
        }

        //obvious
        public bool CalculateBoundaryArea()
        {
            int ptCount = bndLine.Count;
            if (ptCount < 1) return false;

            area = 0;         // Accumulates area in the loop
            int j = ptCount - 1;  // The last vertex is the 'previous' one to the first

            for (int i = 0; i < ptCount; j = i++)
            {
                area += (bndLine[j].easting + bndLine[i].easting) * (bndLine[j].northing - bndLine[i].northing);
            }

            area = Math.Abs(area / 2);

            return area >= 0;
        }
    }
}