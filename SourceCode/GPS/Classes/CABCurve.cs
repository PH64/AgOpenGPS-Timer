﻿using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public class CABCurve
    {
        //pointers to mainform controls
        private readonly FormGPS mf;

        //flag for starting stop adding points
        public bool isBtnCurveOn, isCurveSet, isOkToAddDesPoints;

        public bool isHeadingSameWay = true;

        public double howManyPathsAway;

        public double refHeading, moveDistance;
        private int rA, rB;

        public int currentLocationIndex;

        public double aveLineHeading;

        //the list of points of the ref line.
        public List<vec3> refList = new List<vec3>();
        //the list of points of curve to drive on
        public List<vec3> curList = new List<vec3>();

        public bool isSmoothWindowOpen;
        public List<vec3> smooList = new List<vec3>();

        public List<CCurveLines> curveArr = new List<CCurveLines>();
        public int numCurveLines, numCurveLineSelected;

        public bool isCurveValid;

        public double lastSecond = 0;

        public List<vec3> desList = new List<vec3>();
        public string desName = "**";

        public CABCurve(FormGPS _f)
        {
            //constructor
            mf = _f;
        }

        public void BuildCurveCurrentList(vec3 pivot)
        {
            double minDistA = 1000000, minDistB;
            //move the ABLine over based on the overlap amount set in vehicle
            double widthMinusOverlap = mf.tool.toolWidth - mf.tool.toolOverlap;

            int refCount = refList.Count;
            if (refCount < 5)
            {
                curList?.Clear();
                return;
            }

            //close call hit
            int cc = 0, dd;

            for (int j = 0; j < refCount; j += 10)
            {
                double dist = ((mf.guidanceLookPos.easting - refList[j].easting) * (mf.guidanceLookPos.easting - refList[j].easting))
                                + ((mf.guidanceLookPos.northing - refList[j].northing) * (mf.guidanceLookPos.northing - refList[j].northing));
                if (dist < minDistA)
                {
                    minDistA = dist;
                    cc = j;
                }
            }

            minDistA = minDistB = 1000000;

            dd = cc + 7; if (dd > refCount - 1) dd = refCount;
            cc -= 7; if (cc < 0) cc = 0;

            //find the closest 2 points to current close call
            for (int j = cc; j < dd; j++)
            {
                double dist = ((mf.guidanceLookPos.easting - refList[j].easting) * (mf.guidanceLookPos.easting - refList[j].easting))
                                + ((mf.guidanceLookPos.northing - refList[j].northing) * (mf.guidanceLookPos.northing - refList[j].northing));
                if (dist < minDistA)
                {
                    minDistB = minDistA;
                    rB = rA;
                    minDistA = dist;
                    rA = j;
                }
                else if (dist < minDistB)
                {
                    minDistB = dist;
                    rB = j;
                }
            }

            //reset the line over jump
            mf.gyd.isLateralTriggered = false;

            if (rA >= refCount - 1 || rB >= refCount) return;

            if (rA > rB) { int C = rA; rA = rB; rB = C; }

            //same way as line creation or not
            isHeadingSameWay = Math.PI - Math.Abs(Math.Abs(pivot.heading - refList[rA].heading) - Math.PI) < glm.PIBy2;

            if (mf.yt.isYouTurnTriggered) isHeadingSameWay = !isHeadingSameWay;

            //which side of the closest point are we on is next
            //calculate endpoints of reference line based on closest point
            double easting1 = refList[rA].easting - (Math.Sin(refList[rA].heading) * 100.0);
            double northing1 = refList[rA].northing - (Math.Cos(refList[rA].heading) * 100.0);

            double easting2 = refList[rA].easting + (Math.Sin(refList[rA].heading) * 100.0);
            double northing2 = refList[rA].northing + (Math.Cos(refList[rA].heading) * 100.0);

            //x2-x1
            double dx = easting2 - easting1;
            //z2-z1
            double dz = northing2 - northing1;

            //how far are we away from the reference line at 90 degrees - 2D cross product and distance
            double distanceFromRefLine = ((dz * mf.guidanceLookPos.easting) - (dx * mf.guidanceLookPos.northing) + (easting2
                                * northing1) - (northing2 * easting1))
                                / Math.Sqrt((dz * dz) + (dx * dx));

            double RefDist = (distanceFromRefLine + (isHeadingSameWay ? mf.tool.toolOffset : -mf.tool.toolOffset)) / widthMinusOverlap;
            if (RefDist < 0) howManyPathsAway = (int)(RefDist - 0.5);
            else howManyPathsAway = (int)(RefDist + 0.5);

            //build current list
            isCurveValid = true;

            //build the current line
            curList?.Clear();

            double distAway = widthMinusOverlap * howManyPathsAway + (isHeadingSameWay ? -mf.tool.toolOffset : mf.tool.toolOffset);

            double distSqAway = (distAway * distAway) - 0.01;

            for (int i = 0; i < refCount - 1; i++)
            {
                vec3 point = new vec3(
                refList[i].easting + (Math.Sin(glm.PIBy2 + refList[i].heading) * distAway),
                refList[i].northing + (Math.Cos(glm.PIBy2 + refList[i].heading) * distAway),
                refList[i].heading);
                bool Add = true;
                for (int t = 0; t < refCount; t++)
                {
                    double dist = ((point.easting - refList[t].easting) * (point.easting - refList[t].easting))
                        + ((point.northing - refList[t].northing) * (point.northing - refList[t].northing));
                    if (dist < distSqAway)
                    {
                        Add = false;
                        break;
                    }
                }
                if (Add)
                {
                    if (curList.Count > 0)
                    {
                        double dist = ((point.easting - curList[curList.Count - 1].easting) * (point.easting - curList[curList.Count - 1].easting))
                            + ((point.northing - curList[curList.Count - 1].northing) * (point.northing - curList[curList.Count - 1].northing));
                        if (dist > 1)
                            curList.Add(point);
                    }
                    else curList.Add(point);
                }
            }

            //int cnt;
            //if (style == 1)
            //{
            //    cnt = curList.Count;
            //    vec3[] arr = new vec3[cnt];
            //    cnt--;
            //    curList.CopyTo(arr);
            //    curList.Clear();

            //    //middle points
            //    for (int i = 1; i < cnt; i++)
            //    {
            //        vec3 pt3 = arr[i];
            //        pt3.heading = Math.Atan2(arr[i + 1].easting - arr[i - 1].easting, arr[i + 1].northing - arr[i - 1].northing);
            //        if (pt3.heading < 0) pt3.heading += glm.twoPI;
            //        curList.Add(pt3);
            //    }

            //    return;
            //}

            int cnt = curList.Count;
            if (cnt > 6)
            {
                vec3[] arr = new vec3[cnt];

                curList.CopyTo(arr);

                for (int i = 1; i < (curList.Count - 1); i++)
                {
                    arr[i].easting = (curList[i - 1].easting + curList[i].easting + curList[i + 1].easting) / 3;
                    arr[i].northing = (curList[i - 1].northing + curList[i].northing + curList[i + 1].northing) / 3;
                }
                curList.Clear();

                for (int i = 0; i < (arr.Length - 1); i++)
                {
                    arr[i].heading = Math.Atan2(arr[i + 1].easting - arr[i].easting, arr[i + 1].northing - arr[i].northing);
                    if (arr[i].heading < 0) arr[i].heading += glm.twoPI;
                }

                arr[arr.Length - 1].heading = arr[arr.Length - 2].heading;


                if (mf.tool.isToolTrailing)
                {
                    //depending on hitch is different profile of draft
                    double hitch;
                    if (mf.tool.isToolTBT && mf.tool.toolTankTrailingHitchLength < 0)
                    {
                        hitch = mf.tool.toolTankTrailingHitchLength * 0.85;
                        hitch += mf.tool.toolTrailingHitchLength * 0.65;
                    }
                    else hitch = mf.tool.toolTrailingHitchLength * 1.0;// - mf.vehicle.wheelbase;

                    //move the line forward based on hitch length ratio
                    for (int i = 0; i < arr.Length; i++)
                    {
                        arr[i].easting -= Math.Sin(arr[i].heading) * (hitch);
                        arr[i].northing -= Math.Cos(arr[i].heading) * (hitch);
                    }

                    ////average the points over 3, center weighted
                    //for (int i = 1; i < arr.Length - 2; i++)
                    //{
                    //    arr2[i].easting = (arr[i - 1].easting + arr[i].easting + arr[i + 1].easting) / 3;
                    //    arr2[i].northing = (arr[i - 1].northing + arr[i].northing + arr[i + 1].northing) / 3;
                    //}

                    //recalculate the heading
                    for (int i = 0; i < (arr.Length - 1); i++)
                    {
                        arr[i].heading = Math.Atan2(arr[i + 1].easting - arr[i].easting, arr[i + 1].northing - arr[i].northing);
                        if (arr[i].heading < 0) arr[i].heading += glm.twoPI;
                    }

                    arr[arr.Length - 1].heading = arr[arr.Length - 2].heading;
                }

                //replace the array 
                //curList.AddRange(arr);
                cnt = arr.Length;
                double distance;
                double spacing = 0.5;

                //add the first point of loop - it will be p1
                curList.Add(arr[0]);
                curList.Add(arr[1]);

                for (int i = 0; i < cnt - 3; i++)
                {
                    // add p1
                    curList.Add(arr[i + 1]);

                    distance = glm.Distance(arr[i + 1], arr[i + 2]);

                    if (distance > spacing)
                    {
                        int loopTimes = (int)(distance / spacing + 1);
                        for (int j = 1; j < loopTimes; j++)
                        {
                            curList.Add(new vec3(glm.Catmull(j / (double)(loopTimes), arr[i], arr[i + 1], arr[i + 2], arr[i + 3])));
                        }
                    }
                }

                curList.Add(arr[cnt - 2]);
                curList.Add(arr[cnt - 1]);

                //to calc heading based on next and previous points to give an average heading.
                cnt = curList.Count;
                arr = new vec3[cnt];
                cnt--;
                curList.CopyTo(arr);
                curList.Clear();

                //middle points
                for (int i = 1; i < cnt; i++)
                {
                    double heading = Math.Atan2(arr[i + 1].easting - arr[i - 1].easting, arr[i + 1].northing - arr[i - 1].northing);
                    if (heading < 0) heading += glm.twoPI;
                    curList.Add(new vec3(arr[i].easting, arr[i].northing, heading));
                }
            }
            lastSecond = mf.secondsSinceStart;
        }

        public void GetCurrentCurveLine(vec3 pivot, vec3 steer)
        {
            if (refList == null || refList.Count < 5) return;

            //build new current ref line if required
            if (!isCurveValid || ((mf.secondsSinceStart - lastSecond) > 0.66 
                && (!mf.isAutoSteerBtnOn || mf.mc.steerSwitchValue != 0)))
                BuildCurveCurrentList(pivot);

            if (mf.isStanleyUsed)//Stanley
            {
                mf.gyd.StanleyGuidance(pivot, steer, ref curList, isHeadingSameWay);
            }
            else// Pure Pursuit ------------------------------------------
            {
                mf.gyd.PurePursuitGuidance(pivot, ref curList, isHeadingSameWay);
            }
        }

        public void DrawCurve()
        {
            if (desList.Count > 0)
            {
                GL.Color3(0.95f, 0.42f, 0.750f);
                GL.Begin(PrimitiveType.LineStrip);
                for (int h = 0; h < desList.Count; h++) GL.Vertex3(desList[h].easting, desList[h].northing, 0);
                GL.End();
            }


            int ptCount = refList.Count;
            if (refList.Count == 0) return;

            GL.LineWidth(mf.ABLine.lineWidth);
            GL.Color3(0.96, 0.2f, 0.2f);
            GL.Begin(PrimitiveType.Lines);
            for (int h = 0; h < ptCount; h++) GL.Vertex3(refList[h].easting, refList[h].northing, 0);
            if (!mf.curve.isCurveSet)
            {
                GL.Color3(0.930f, 0.0692f, 0.260f);
                ptCount--;
                GL.Vertex3(refList[ptCount].easting, refList[ptCount].northing, 0);
                GL.Vertex3(mf.pivotAxlePos.easting, mf.pivotAxlePos.northing, 0);
            }
            GL.End();

            //GL.PointSize(8.0f);
            //GL.Begin(PrimitiveType.Points);
            //GL.Color3(1.0f, 1.0f, 0.0f);
            ////GL.Vertex3(goalPointAB.easting, goalPointAB.northing, 0.0);
            //GL.Vertex3(mf.gyd.rEastSteer, mf.gyd.rNorthSteer, 0.0);
            //GL.Color3(1.0f, 0.0f, 1.0f);
            //GL.Vertex3(mf.gyd.rEastPivot, mf.gyd.rNorthPivot, 0.0);
            //GL.End();
            //GL.PointSize(1.0f);




            if (mf.font.isFontOn && refList.Count > 410)
            {
                GL.Color3(0.40f, 0.90f, 0.95f);
                mf.font.DrawText3D(refList[201].easting, refList[201].northing, "&A");
                mf.font.DrawText3D(refList[refList.Count - 200].easting, refList[refList.Count - 200].northing, "&B");
            }

            //just draw ref and smoothed line if smoothing window is open
            if (isSmoothWindowOpen)
            {
                ptCount = smooList.Count;
                if (smooList.Count == 0) return;

                GL.LineWidth(mf.ABLine.lineWidth);
                GL.Color3(0.930f, 0.92f, 0.260f);
                GL.Begin(PrimitiveType.Lines);
                for (int h = 0; h < ptCount; h++) GL.Vertex3(smooList[h].easting, smooList[h].northing, 0);
                GL.End();
            }
            else //normal. Smoothing window is not open.
            {
                ptCount = curList.Count;
                if (ptCount > 0 && isCurveSet)
                {
                    GL.PointSize(2);

                    GL.Color3(0.95f, 0.2f, 0.95f);
                    GL.Begin(PrimitiveType.LineStrip);
                    for (int h = 0; h < ptCount; h++) GL.Vertex3(curList[h].easting, curList[h].northing, 0);
                    GL.End();

                    mf.yt.DrawYouTurn();
                }
            }
            GL.PointSize(1.0f);


            //if (isEditing)
            //{
            //    int ptCount = refList.Count;
            //    if (refList.Count == 0) return;

            //    GL.LineWidth(mf.ABLine.lineWidth);
            //    GL.Color3(0.930f, 0.2f, 0.260f);
            //    GL.Begin(PrimitiveType.Lines);
            //    for (int h = 0; h < ptCount; h++) GL.Vertex3(refList[h].easting, refList[h].northing, 0);
            //    GL.End();

            //    //current line
            //    if (curList.Count > 0 && isCurveSet)
            //    {
            //        ptCount = curList.Count;
            //        GL.Color3(0.95f, 0.2f, 0.950f);
            //        GL.Begin(PrimitiveType.LineStrip);
            //        for (int h = 0; h < ptCount; h++) GL.Vertex3(curList[h].easting, curList[h].northing, 0);
            //        GL.End();
            //    }


            //if (mf.camera.camSetDistance > -200)
            //{
            //    double toolWidth2 = mf.tool.toolWidth - mf.tool.toolOverlap;
            //    double cosHeading2 = Math.Cos(-mf.curve.aveLineHeading);
            //    double sinHeading2 = Math.Sin(-mf.curve.aveLineHeading);

            //    GL.Color3(0.8f, 0.3f, 0.2f);
            //    GL.PointSize(2);
            //    GL.Begin(PrimitiveType.Points);

            //    ptCount = refList.Count;
            //    for (int i = 1; i <= 6; i++)
            //    {
            //        for (int h = 0; h < ptCount; h++)
            //            GL.Vertex3((cosHeading2 * toolWidth2) + mf.curve.refList[h].easting,
            //                          (sinHeading2 * toolWidth2) + mf.curve.refList[h].northing, 0);
            //        toolWidth2 = toolWidth2 + mf.tool.toolWidth - mf.tool.toolOverlap;
            //    }

            //    GL.End();
            //}
            //}
        }

        public void BuildTram()
        {
            mf.tram.BuildTramBnd();
            mf.tram.tramList?.Clear();
            mf.tram.tramArr?.Clear();

            bool isBndExist = mf.bnd.bndArr.Count != 0;

            double pass = 0.5;

            int refCount = refList.Count;

            int cntr = 0;
            if (isBndExist) cntr = 1;

            for (int i = cntr; i <= mf.tram.passes; i++)
            {
                double distSqAway = (mf.tram.tramWidth * (i + 0.5) - mf.tram.halfWheelTrack + mf.tool.halfToolWidth)
                        * (mf.tram.tramWidth * (i + 0.5) - mf.tram.halfWheelTrack + mf.tool.halfToolWidth) * 0.999999;

                mf.tram.tramArr = new List<vec2>();
                mf.tram.tramList.Add(mf.tram.tramArr);
                for (int j = 0; j < refCount; j += 1)
                {
                    vec2 point = new vec2(
                    (Math.Sin(glm.PIBy2 + refList[j].heading) *
                        ((mf.tram.tramWidth * (pass + i)) - mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + refList[j].easting,
                    (Math.Cos(glm.PIBy2 + refList[j].heading) *
                        ((mf.tram.tramWidth * (pass + i)) - mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + refList[j].northing);

                    bool Add = true;
                    for (int t = 0; t < refCount; t++)
                    {
                        //distance check to be not too close to ref line
                        double dist = ((point.easting - refList[t].easting) * (point.easting - refList[t].easting))
                            + ((point.northing - refList[t].northing) * (point.northing - refList[t].northing));
                        if (dist < distSqAway)
                        {
                            Add = false;
                            break;
                        }
                    }
                    if (Add)
                    {
                        if (isBndExist)
                        {
                            if (mf.tram.tramArr.Count > 0)
                            {
                                //a new point only every 2 meters
                                double dist = ((point.easting - mf.tram.tramArr[mf.tram.tramArr.Count - 1].easting) * (point.easting - mf.tram.tramArr[mf.tram.tramArr.Count - 1].easting))
                                    + ((point.northing - mf.tram.tramArr[mf.tram.tramArr.Count - 1].northing) * (point.northing - mf.tram.tramArr[mf.tram.tramArr.Count - 1].northing));
                                if (dist > 2)
                                {
                                    //if inside the boundary, add
                                    if (mf.bnd.bndArr[0].IsPointInsideBoundaryEar(point))
                                    {
                                        mf.tram.tramArr.Add(point);
                                    }
                                }
                            }
                            else if (mf.bnd.bndArr[0].IsPointInsideBoundaryEar(point))
                            {
                                mf.tram.tramArr.Add(point);
                            }
                        }
                        else
                        {
                            //no boundary to cull points
                            if (mf.tram.tramArr.Count > 0)
                            {
                                double dist = ((point.easting - mf.tram.tramArr[mf.tram.tramArr.Count - 1].easting) * (point.easting - mf.tram.tramArr[mf.tram.tramArr.Count - 1].easting))
                                    + ((point.northing - mf.tram.tramArr[mf.tram.tramArr.Count - 1].northing) * (point.northing - mf.tram.tramArr[mf.tram.tramArr.Count - 1].northing));
                                if (dist > 2)
                                {
                                    mf.tram.tramArr.Add(point);
                                }
                            }
                            else
                            {
                                mf.tram.tramArr.Add(point);
                            }

                        }
                    }

                }
            }

            for (int i = cntr; i <= mf.tram.passes; i++)
            {
                double distSqAway = (mf.tram.tramWidth * (i + 0.5) + mf.tram.halfWheelTrack + mf.tool.halfToolWidth)
                        * (mf.tram.tramWidth * (i + 0.5) + mf.tram.halfWheelTrack + mf.tool.halfToolWidth) * 0.999999;

                mf.tram.tramArr = new List<vec2>();
                mf.tram.tramList.Add(mf.tram.tramArr);
                for (int j = 0; j < refCount; j += 1)
                {
                    vec2 point = new vec2(
                    (Math.Sin(glm.PIBy2 + refList[j].heading) *
                        ((mf.tram.tramWidth * (pass + i)) + mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + refList[j].easting,
                    (Math.Cos(glm.PIBy2 + refList[j].heading) *
                        ((mf.tram.tramWidth * (pass + i)) + mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + refList[j].northing);

                    bool Add = true;
                    for (int t = 0; t < refCount; t++)
                    {
                        //distance check to be not too close to ref line
                        double dist = ((point.easting - refList[t].easting) * (point.easting - refList[t].easting))
                            + ((point.northing - refList[t].northing) * (point.northing - refList[t].northing));
                        if (dist < distSqAway)
                        {
                            Add = false;
                            break;
                        }
                    }
                    if (Add)
                    {
                        if (isBndExist)
                        {
                            if (mf.tram.tramArr.Count > 0)
                            {
                                //a new point only every 2 meters
                                double dist = ((point.easting - mf.tram.tramArr[mf.tram.tramArr.Count - 1].easting) * (point.easting - mf.tram.tramArr[mf.tram.tramArr.Count - 1].easting))
                                    + ((point.northing - mf.tram.tramArr[mf.tram.tramArr.Count - 1].northing) * (point.northing - mf.tram.tramArr[mf.tram.tramArr.Count - 1].northing));
                                if (dist > 2)
                                {
                                    //if inside the boundary, add
                                    if (mf.bnd.bndArr[0].IsPointInsideBoundaryEar(point))
                                    {
                                        mf.tram.tramArr.Add(point);
                                    }
                                }
                            }
                            else
                            {
                                //need a first point to do distance
                                if (mf.bnd.bndArr[0].IsPointInsideBoundaryEar(point))
                                {
                                    mf.tram.tramArr.Add(point);
                                }
                            }
                        }
                        else
                        {
                            //no boundary to cull points
                            if (mf.tram.tramArr.Count > 0)
                            {
                                double dist = ((point.easting - mf.tram.tramArr[mf.tram.tramArr.Count - 1].easting) * (point.easting - mf.tram.tramArr[mf.tram.tramArr.Count - 1].easting))
                                    + ((point.northing - mf.tram.tramArr[mf.tram.tramArr.Count - 1].northing) * (point.northing - mf.tram.tramArr[mf.tram.tramArr.Count - 1].northing));
                                if (dist > 2)
                                {
                                    mf.tram.tramArr.Add(point);
                                }
                            }
                            else
                            {
                                mf.tram.tramArr.Add(point);
                            }

                        }
                    }
                }
            }
        }

        //for calculating for display the averaged new line
        public void SmoothAB(int smPts)
        {
            //count the reference list of original curve
            int cnt = refList.Count;

            //just go back if not very long
            if (!isCurveSet || cnt < 400) return;

            //make a list to draw
            smooList?.Clear();

            //read the points before and after the setpoint
            for (int s = 0; s < smPts / 2; s++)
            {
                smooList.Add(new vec3(refList[s]));
            }

            //average them - center weighted average
            for (int i = smPts / 2; i < cnt - (smPts / 2); i++)
            {
                double easting = 0;
                double northing = 0;

                for (int j = -smPts / 2; j < smPts / 2; j++)
                {
                    easting += refList[j + i].easting;
                    northing += refList[j + i].northing;
                }
                easting /= smPts;
                northing /= smPts;

                smooList.Add(new vec3(easting, northing, refList[i].heading));
            }

            for (int s = cnt - (smPts / 2); s < cnt; s++)
            {
                smooList.Add(new vec3(refList[s]));
            }
        }

        public void CalculateTurnHeadings(ref List<vec3> Points)
        {
            //to calc heading based on next and previous points to give an average heading.
            int cnt = Points.Count;
            if (cnt > 0)
            {
                vec3[] arr = new vec3[cnt];
                cnt--;
                Points.CopyTo(arr);
                Points.Clear();

                //middle points
                for (int i = 1; i < cnt; i++)
                {
                    double heading = Math.Atan2(arr[i + 1].easting - arr[i - 1].easting, arr[i + 1].northing - arr[i - 1].northing);
                    if (heading < 0) heading += glm.twoPI;
                    Points.Add(new vec3(arr[i].easting, arr[i].northing, heading));
                }
            }
        }

        //turning the visual line into the real reference line to use
        public void SaveSmoothAsRefList()
        {
            //oops no smooth list generated
            int cnt = smooList.Count;
            if (cnt == 0) return;

            //eek
            refList?.Clear();

            //copy to an array to calculate all the new headings
            vec3[] arr = new vec3[cnt];
            smooList.CopyTo(arr);

            //calculate new headings on smoothed line
            for (int i = 1; i < cnt - 1; i++)
            {
                double heading = Math.Atan2(arr[i + 1].easting - arr[i - 1].easting, arr[i + 1].northing - arr[i - 1].northing);
                if (heading < 0) heading += glm.twoPI;
                refList.Add(new vec3(arr[i].easting, arr[i].northing, heading));
            }
        }

        public void MoveABCurve(double dist)
        {
            isCurveValid = false;
            lastSecond = 0;

            int cnt = refList.Count;
            vec3[] arr = new vec3[cnt];
            refList.CopyTo(arr);
            refList.Clear();

            moveDistance += isHeadingSameWay ? dist : -dist;

            for (int i = 0; i < cnt; i++)
            {
                arr[i].easting += Math.Cos(arr[i].heading) * (isHeadingSameWay ? dist : -dist);
                arr[i].northing -= Math.Sin(arr[i].heading) * (isHeadingSameWay ? dist : -dist);
                refList.Add(arr[i]);
            }
        }

        public bool PointOnLine(vec3 pt1, vec3 pt2, vec3 pt)
        {
            if (pt1.northing == pt2.northing && pt1.easting == pt2.easting) { pt1.northing -= 0.00001; }

            double U = ((pt.northing - pt1.northing) * (pt2.northing - pt1.northing)) + ((pt.easting - pt1.easting) * (pt2.easting - pt1.easting));

            double Udenom = Math.Pow(pt2.northing - pt1.northing, 2) + Math.Pow(pt2.easting - pt1.easting, 2);

            U /= Udenom;

            vec2 r = new vec2(pt1.northing + U * (pt2.northing - pt1.northing), pt1.easting + U * (pt2.easting - pt1.easting));

            double minx, maxx, miny, maxy;

            minx = Math.Min(pt1.northing, pt2.northing);
            maxx = Math.Max(pt1.northing, pt2.northing);

            miny = Math.Min(pt1.easting, pt2.easting);
            maxy = Math.Max(pt1.easting, pt2.easting);
            return _ = r.northing >= minx && r.northing <= maxx && (r.easting >= miny && r.easting <= maxy);
        }

        //add extensons
        public void AddFirstLastPoints()
        {
            vec3 end = refList[refList.Count - 1];
            for (int i = 1; i < 200; i++)
            {
                refList.Add(new vec3(end.easting + Math.Sin(end.heading) * i, end.northing + Math.Cos(end.heading) * i, end.heading));
            }

            //and the beginning
            vec3 start = new vec3(refList[0]);
            for (int i = 1; i < 200; i++)
            {
                refList.Insert(0, new vec3(start.easting - Math.Sin(start.heading) * i, start.northing - Math.Cos(start.heading) * i, start.heading));
            }
        }

        public void ResetCurveLine()
        {
            curList?.Clear();
            refList?.Clear();
            isCurveSet = false;
        }

        ////draw the guidance line
    }

    public class CCurveLines
    {
        public List<vec3> curvePts = new List<vec3>();
        public double aveHeading = 3;
        public string Name = "aa";
    }
}


//for (int i = 1; i <= mf.tram.passes; i++)
//{
//    tramArr = new List<vec2>();
//    tramList.Add(tramArr);

//    List<vec2> tramTemp = new List<vec2>();

//    for (int j = 0; j < tramRef.Count; j++)
//    {
//        P1.easting = (hsin * ((mf.tram.tramWidth * (pass + i)) - mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + tramRef[j].easting;
//        P1.northing = (hcos * ((mf.tram.tramWidth * (pass + i)) - mf.tram.halfWheelTrack + mf.tool.halfToolWidth)) + tramRef[j].northing;

//        if (isBndExist)
//        {
//            if (mf.bnd.bndArr[0].IsPointInsideBoundaryEar(P1))
//            {
//                tramTemp.Add(P1);
//                P1.easting = (hsin * mf.vehicle.trackWidth) + P1.easting;
//                P1.northing = (hcos * mf.vehicle.trackWidth) + P1.northing;
//                tramTemp.Add(P1);
//            }
//        }
//        else
//        {
//            tramTemp.Add(P1);

//            P1.easting = (hsin * mf.vehicle.trackWidth) + P1.easting;
//            P1.northing = (hcos * mf.vehicle.trackWidth) + P1.northing;
//            tramTemp.Add(P1);
//        }

//        if (tramTemp.Count > 6)
//        {
//            vec2[] array = new vec2[tramTemp.Count];
//            tramTemp.CopyTo(array);

//            tramArr.Add(array[0]);
//            tramArr.Add(array[1]);
//            tramArr.Add(array[tramTemp.Count - 2]);
//            tramArr.Add(array[tramTemp.Count - 1]);
//        }

//    }
//}

