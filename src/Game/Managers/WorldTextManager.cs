#region license

//  Copyright (C) 2019 ClassicUO Development Community on Github
//
//	This project is an alternative client for the game Ultima Online.
//	The goal of this is to develop a lightweight client considering 
//	new technologies.  
//      
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;

using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Scenes;
using ClassicUO.Input;
using ClassicUO.IO;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using ClassicUO.Utility.Collections;

using Microsoft.Xna.Framework;

using IUpdateable = ClassicUO.Interfaces.IUpdateable;

namespace ClassicUO.Game.Managers
{
    internal class WorldTextManager : IUpdateable
    {
        private readonly Dictionary<Serial, OverheadDamage> _damages = new Dictionary<Serial, OverheadDamage>();
        //private readonly List<GameObject> _staticToUpdate = new List<GameObject>();
        private readonly List<Tuple<Serial, Serial>> _subst = new List<Tuple<Serial, Serial>>();
        private readonly List<Serial> _toRemoveDamages = new List<Serial>();


        private readonly List<Rectangle> _bounds = new List<Rectangle>();
        private MessageInfo _firstNode = new MessageInfo(), _drawPointer;

        public void Update(double totalMS, double frameMS)
        {
            ProcessWorldText(false);

            UpdateDamageOverhead(totalMS, frameMS);

            if (_toRemoveDamages.Count > 0)
            {
                foreach ( Serial s in _toRemoveDamages)
                {
                    _damages.Remove(s);
                }

                _toRemoveDamages.Clear();
            }
        }



        private void DrawTextOverheads(UltimaBatcher2D batcher, int startX, int startY, float scale, int renderIndex)
        {
            int screenX = Engine.Profile.Current.GameWindowPosition.X;
            int screenY = Engine.Profile.Current.GameWindowPosition.Y;
            int screenW = Engine.Profile.Current.GameWindowSize.X;
            int screenH = Engine.Profile.Current.GameWindowSize.Y;
            
         
            ProcessWorldText(false);

            bool health = Engine.Profile.Current.ShowMobilesHP;
            int alwaysHP = Engine.Profile.Current.MobileHPShowWhen;
            int mode = Engine.Profile.Current.MobileHPType;
            int mouseX = Mouse.Position.X;
            int mouseY = Mouse.Position.Y;

            for (var o = _drawPointer; o != null; o = o.Left)
            {
                if (o.RenderedText == null || o.RenderedText.IsDestroyed || o.Time < Engine.Ticks /*|| o.Parent.Parent.Tile == null*/)
                    continue;

                var parent = o.Owner;

                int x = startX + parent.RealScreenPosition.X;
                int y = startY + parent.RealScreenPosition.Y;

                int offY = 0;

                if (parent is Mobile m)
                {
                    if (health && mode != 1 && ((alwaysHP >= 1 && m.Hits != m.HitsMax) || alwaysHP == 0))
                    {
                        offY += 22;
                    }

                    if (!m.IsMounted)
                        offY -= 22;

                    FileManager.Animations.GetAnimationDimensions(m.AnimIndex, 
                                                                  m.GetGraphicForAnimation(), 
                                                                  /*(byte) m.GetDirectionForAnimation()*/ 0,
                                                                  /*Mobile.GetGroupForAnimation(m, isParent:true)*/ 0, 
                                                                  m.IsMounted,
                                                                  /*(byte) m.AnimIndex*/ 0, 
                                                                  out _,
                                                                  out int centerY,
                                                                  out _,
                                                                  out int height);
                    x += (int)m.Offset.X;
                    x += 22;
                    y += (int)(m.Offset.Y - m.Offset.Z - (height + centerY + 8));
                }
                else if (parent.Texture != null)
                {
                    if (renderIndex != parent.UseInRender)
                        continue;

                    switch (parent)
                    {
                        case Item _: offY = -22;

                            if (parent.Texture is ArtTexture t)
                                y -= t.ImageRectangle.Height >> 1;
                            else
                                y -= parent.Texture.Height >> 1;

                            break;

                        case Static _:
                        case Multi _: offY = -44;

                            if (parent.Texture is ArtTexture t1)
                                y -= t1.ImageRectangle.Height >> 1;
                            else
                                y -= parent.Texture.Height >> 1;

                            break;

                        default:
                            y -= parent.Texture.Height >> 1;
                            break;
                    }

                    x += 22;
                }

                x = (int)(x / scale);
                y = (int)(y / scale);

                x -= (int)(screenX / scale);
                y -= (int)(screenY / scale);

                x += screenX;
                y += screenY;



                int minX = screenX + (o.RenderedText.Width >> 1) + 6;
                int maxX = screenX + screenW - ((o.RenderedText.Width >> 1) - 3);

                if (x < minX)
                    x = minX;
                else if (x > maxX)
                    x = maxX;


                int minY = screenY + parent.TextContainer.TotalHeight + offY;
                int maxY = screenY + screenH + offY + 6;

                if (y < minY)
                    y = minY;
                else if (y > maxY)
                    y = maxY;

                offY += o.OffsetY;

                o.X = x - (o.RenderedText.Width >> 1);
                o.Y = y - offY - o.RenderedText.Height;


                ushort hue = 0;

                float alpha = 1f - o.Alpha / 255f;

                if (o.IsTransparent)
                {
                    if (o.Alpha == 0xFF)
                        alpha = 1f - 0x7F / 255f;
                }

                if (o.RenderedText.Texture.Contains(mouseX - o.X, mouseY - o.Y))
                {
                    SelectedObject.Object = o;
                }

                if (SelectedObject.LastObject == o)
                {
                    hue = 0x35;
                }


                o.RenderedText.Draw(batcher, o.X, o.Y, alpha, hue);
            }
        }

        public void Select(int renderIndex)
        {
            //ProcessWorldText(false);

            //int mouseX = Mouse.Position.X;
            //int mouseY = Mouse.Position.Y;

            //for (var item = _drawPointer; item != null; item = item.Left)
            //{
            //    if (item.RenderedText == null || item.RenderedText.IsDestroyed)
            //        continue;

            //    if (item.Time >= Engine.Ticks)
            //    {
            //        if (item.Owner == null || item.Owner.UseInRender != renderIndex)
            //            continue;
            //    }

            //    if (item.RenderedText.Texture.Contains(mouseX - item.X, mouseY - item.Y))
            //    {
            //        SelectedObject.Object = item;
            //    }
            //}
        }

        public void MoveToTopIfSelected()
        {
            if (_firstNode != null && SelectedObject.LastObject is MessageInfo msg)
            {
                if (msg.Right != null)
                    msg.Right.Left = msg.Left;

                if (msg.Left != null)
                    msg.Left.Right = msg.Right;

                msg.Left = msg.Right = null;


                var next = _firstNode.Right;
                _firstNode.Right = msg;
                msg.Left = _firstNode;
                msg.Right = next;

                if (next != null)
                    next.Left = msg;
            }
        }

        public void ProcessWorldText(bool doit)
        {
            if (doit)
            {
                if (_bounds.Count != 0)
                    _bounds.Clear();
            }

            for (_drawPointer = _firstNode; _drawPointer != null; _drawPointer = _drawPointer.Right)
            {
                var t = _drawPointer;

                if (doit)
                {
                    if (t.Time >= Engine.Ticks && !t.RenderedText.IsDestroyed)
                    {
                        if (t.Owner != null)
                        {
                            t.IsTransparent = Collides(t);
                            CalculateAlpha(t);
                        }
                    }
                }

                if (_drawPointer.Right == null)
                    break;
            }

        }

        private void CalculateAlpha(MessageInfo msg)
        {
            int delta = (int) (msg.Time - Engine.Ticks);

            if (delta >= 0 && delta <= 1000)
            {
                delta /= 10;

                if (delta > 100)
                    delta = 100;
                else if (delta < 1)
                    delta = 0;

                delta = (255 * delta) / 100;

                if (!msg.IsTransparent || delta <= 0x7F)
                {
                    msg.Alpha = (byte) delta;
                }

                msg.IsTransparent = true;
            }
        }

        private bool Collides(MessageInfo msg)
        {
            bool result = false;

            Rectangle rect = new Rectangle()
            {
                X = msg.X,
                Y = msg.Y,
                Width = msg.RenderedText.Width,
                Height = msg.RenderedText.Height
            };

            for (int i = 0; i < _bounds.Count; i++)
            {
                if (_bounds[i].Intersects(rect))
                {
                    result = true;
                    break;
                }
            }

            _bounds.Add(rect);
            return result;
        }

        public void AddMessage(MessageInfo obj)
        {
            if (obj == null)
                return;

            //if ((obj.Owner is Static || obj.Owner is Multi))
            //{
            //    if (!_staticToUpdate.Contains(obj.Owner))
            //        _staticToUpdate.Add(obj.Owner);
            //}
            
            var item = _firstNode;

            if (item != null)
            {
                if (item.Right != null)
                {
                    var next = item.Right;

                    item.Right = obj;
                    obj.Left = item;
                    obj.Right = next;
                    next.Left = obj;
                }
                else
                {
                    item.Right = obj;
                    obj.Left = item;
                    obj.Right = null;
                }
            }
        }

        public bool Draw(UltimaBatcher2D batcher, int startX, int startY, int renderIndex)
        {
            float scale = Engine.SceneManager.GetScene<GameScene>().Scale;

            DrawTextOverheads(batcher, startX, startY, scale, renderIndex);

            foreach (KeyValuePair<Serial, OverheadDamage> overheadDamage in _damages)
            {
                int x = startX;
                int y = startY;

                Entity mob = World.Get(overheadDamage.Key);

                if (mob == null || mob.IsDestroyed)
                {
                    uint ser = overheadDamage.Key | 0x8000_0000;

                    if (World.CorpseManager.Exists(0, ser))
                    {
                        Item item = World.CorpseManager.GetCorpseObject(ser);

                        if (item != null && item != overheadDamage.Value.Parent)
                        {
                            _subst.Add(Tuple.Create(overheadDamage.Key, item.Serial));
                            overheadDamage.Value.SetParent(item);
                        }
                    }
                    else
                        continue;
                }

                overheadDamage.Value.Draw(batcher, x, y, scale);
            }

            return true;
        }

        private void UpdateDamageOverhead(double totalMS, double frameMS)
        {
            if (_subst.Count != 0)
            {
                foreach (Tuple<Serial, Serial> tuple in _subst)
                {
                    if (_damages.TryGetValue(tuple.Item1, out var dmg))
                    {
                        _damages.Remove(tuple.Item1);
                        _damages.Add(tuple.Item2, dmg);
                    }
                }

                _subst.Clear();
            }

            foreach (KeyValuePair<Serial, OverheadDamage> overheadDamage in _damages)
            {
                overheadDamage.Value.Update();

                if (overheadDamage.Value.IsEmpty) _toRemoveDamages.Add(overheadDamage.Key);
            }
        }


        internal void AddDamage(Serial obj, int dmg)
        {
            if (!_damages.TryGetValue(obj, out var dm) || dm == null)
            {
                dm = new OverheadDamage(World.Get(obj));
                _damages[obj] = dm;
            }

            dm.Add(dmg);
        }

        public void Clear()
        {
            if (_toRemoveDamages.Count > 0)
            {
                foreach (Serial s in _toRemoveDamages)
                {
                    _damages.Remove(s);
                }

                _toRemoveDamages.Clear();
            }

            _subst.Clear();

            //_staticToUpdate.Clear();

            _firstNode = new MessageInfo();
            _drawPointer = null;
        }
    }
}