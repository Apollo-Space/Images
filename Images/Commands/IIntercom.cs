﻿using System;
using System.Collections.Generic;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using MEC;

namespace Images.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class IIntercom : ICommand
    {
        public string Command => "iintercom";
        public string[] Aliases => new string[] {"imageintercom"};
        public string Description => "Set the intercom text to an image.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (sender.CheckPermission("images.iintercom") && arguments.Array != null && arguments.Array.Length > 1 && (arguments.Array[1].Trim().ToLower() == "reset" || arguments.Array[1].Trim().ToLower() == "none"))
            {
                Intercom.DisplayText = "";
                Images.Singleton.IntercomText = null;
                response = "Reset intercom.";
                return true;
            }
            
            HandleCommandObject obj = Util.HandleCommand(arguments, sender, out response, false, "iintercom", "images.iintercom");
            if (obj == null) return true;

            Timing.KillCoroutines(Images.Singleton.IntercomHandle);
            Images.Singleton.IntercomHandle = Timing.RunCoroutine(ShowIntercom(obj));
            Images.Singleton.Coroutines.Add(Images.Singleton.IntercomHandle);

            response = "Successfully set intercom text.";
            return true;
        }

        private IEnumerator<float> ShowIntercom(HandleCommandObject obj)
        {
            var frames = new List<string>();
            
            var handle = new CoroutineHandle();
            try
            {
                handle = Util.LocationToText(obj.image["location"], text =>
                    {
                        Images.Singleton.IntercomText = text.Replace("\\n", "\n");
                        Intercom.DisplayText = Images.Singleton.IntercomText;
                        frames.Add(Images.Singleton.IntercomText);
                    }, obj.image["name"].Trim().ToLower(), obj.image["isURL"] == "true", obj.scale, true, 1/obj.fps);
                Images.Singleton.Coroutines.Add(handle);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            yield return Timing.WaitUntilDone(handle);

            var cur = 0;

            if (frames.Count <= 1) yield break;
            while (true)
            {
                Images.Singleton.IntercomText = frames[cur % frames.Count];
                Intercom.DisplayText = Images.Singleton.IntercomText;

                yield return Timing.WaitForSeconds(1/obj.fps);

                cur++;
            }
        }
    }
}