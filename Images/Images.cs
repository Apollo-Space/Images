using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using HarmonyLib;
using MEC;

namespace Images
{
    public class Images : Plugin<Config>
    {
        public override string Name { get; } = "Images";
        public override string Author { get; } = "PintTheDragon";
        public override Version Version { get; } = new Version(1, 1, 3);

        internal static Images Singleton;
        internal string IntercomText = null;
        internal Dictionary<string, CachedImage> ImageCache = new Dictionary<string, CachedImage>();
        internal List<CoroutineHandle> Coroutines = new List<CoroutineHandle>();
        internal bool CacheReady = true;
        
        internal CoroutineHandle IntercomHandle;
        internal CoroutineHandle HintHandle;
        internal CoroutineHandle BroadcastHandle;

        private Harmony harmony;
        private CoroutineHandle preCache;

        public override void OnEnabled()
        {
            base.OnEnabled();
            
            if(Config.EnablePrecache) CacheReady = false;

            Singleton = this;
            
            harmony = new Harmony("PintImages");
            harmony.PatchAll();

            Exiled.Events.Handlers.Server.RestartingRound += OnRoundRestart;
            Exiled.Events.Handlers.Server.ReloadedConfigs += OnConfigReloaded;

            preCache = Timing.RunCoroutine(RunPreCache());
        }

        public override void OnDisabled()
        {
            base.OnDisabled();
            
            if(Config.EnablePrecache) CacheReady = false;

            foreach (var coroutineHandle in Coroutines)
            {
                Timing.KillCoroutines(coroutineHandle);
            }
            Coroutines.Clear();

            Singleton = null;
            
            harmony.UnpatchAll();
            
            Exiled.Events.Handlers.Server.RestartingRound -= OnRoundRestart;
            Exiled.Events.Handlers.Server.ReloadedConfigs -= OnConfigReloaded;

            ImageCache.Clear();
        }

        private void OnConfigReloaded()
        {
            if(Config.EnablePrecache) CacheReady = false;

            foreach (var coroutineHandle in Coroutines)
            {
                Timing.KillCoroutines(coroutineHandle);
            }
            Coroutines.Clear();
            
            ImageCache.Clear();
            preCache = Timing.RunCoroutine(RunPreCache());
        }

        private void OnRoundRestart()
        {
            foreach (var coroutineHandle in Coroutines)
            {
                Timing.KillCoroutines(coroutineHandle);
            }
            Coroutines.Clear();
        }

        private IEnumerator<float> RunPreCache()
        {
            if (!Config.EnablePrecache) yield break;
            
            foreach (var image in Config.Images)
            {
                if (image.ContainsKey("precache") && image["precache"].Trim().ToLower() == "true")
                {
                    var scale = 0;

                    if (image.ContainsKey("scale") && image["scale"].Trim().ToLower() != "auto" && !int.TryParse(image["scale"].Trim().ToLower(), out scale))
                    {
                        Log.Error("The scale value for the custom intercom image is incorrect. Use an integer or \"auto\".");
                        continue;
                    }
                    
                    var compress = true;

                    if (image.ContainsKey("compress") && !bool.TryParse(image["compress"].Trim().ToLower(), out compress))
                    {
                        Log.Error("The compress parameter for this image is invalid. Only use booleans");
                        continue;
                    }

                    var handle = Util.LocationToText(image["location"], text => {}, image["name"].Trim().ToLower(), image["isURL"] == "true", scale, true, 1f, compress);
                    Coroutines.Add(handle);
                    yield return Timing.WaitUntilDone(handle);

                    yield return Timing.WaitForSeconds(10f);
                    
                    handle = Util.LocationToText(image["location"], text => {}, image["name"].Trim().ToLower(), image["isURL"] == "true", scale, false, 1f, compress);
                    Coroutines.Add(handle);
                    yield return Timing.WaitUntilDone(handle);
                }
            }

            CacheReady = true;
            
            Log.Info("Finished pre-caching items!");
        }

        //what
        internal void RunIntercomImage(string imageName)
        {
            if (imageName == "none" || Config.Images.Count(img => img["name"].Trim().ToLower().Replace(" ", "") == imageName) <= 0) return;
            
            var image = Config.Images.First(img => img["name"].Trim().ToLower().Replace(" ", "") == imageName);
                
            var scale = 0;

            if (image.ContainsKey("scale") && image["scale"].Trim().ToLower() != "auto" && !int.TryParse(image["scale"].Trim().ToLower(), out scale))
            {
                Log.Error("The scale value for the custom intercom image is incorrect. Use an integer or \"auto\".");
                return;
            }
                
            var fps = 10;

            if (image.ContainsKey("fps") && image["fps"].Trim().ToLower() != "auto" && !int.TryParse(image["fps"].Trim().ToLower(), out fps))
            {
                Log.Error("The fps value for the custom intercom image is incorrect. Use an integer.");
                return;
            }

            Timing.KillCoroutines(IntercomHandle);
                
            IntercomText = null;
            Intercom.DisplayText = "";
                
            IntercomHandle = Timing.RunCoroutine(ShowIntercom(image, scale, fps));
            Coroutines.Add(IntercomHandle);
        }

        private IEnumerator<float> ShowIntercom(Dictionary<string, string> image, int scale, float fps)
        {
            yield return Timing.WaitUntilDone(preCache);
            
            List<string> frames = new List<string>();

            CoroutineHandle handle = new CoroutineHandle();
            try
            {
                handle = Util.LocationToText(image["location"], text =>
                    {
                        IntercomText = text.Replace("\\n", "\n");
                        Intercom.DisplayText = IntercomText;
                        frames.Add(IntercomText);
                    }, image["name"].Trim().ToLower(), image["isURL"] == "true", scale, true, 1/fps);
                
                Coroutines.Add(handle);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            yield return Timing.WaitUntilDone(handle);

            var cur = 0;

            if (frames.Count > 1)
            {
                while (true)
                {
                    IntercomText = frames[cur % frames.Count];
                    Intercom.DisplayText = IntercomText;

                    yield return Timing.WaitForSeconds(1/fps);

                    cur++;
                }
            }
        }
    }
}