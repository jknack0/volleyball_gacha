using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VG.Unity;

namespace VG.Tests
{
    /// <summary>
    /// VB-12 smoke: the self-bootstrapping grey-box actually drives the tick sim into the view.
    /// Defends: RuntimeInitializeOnLoadMethod boot, fixed-tick driving, sim→ball-transform flow.
    /// </summary>
    public class GreyBoxSmokeTests
    {
        [UnityTest]
        public IEnumerator GreyBox_SelfBoots_AndTheBallFlies()
        {
            yield return null; // let the RuntimeInitializeOnLoadMethod boot + Awake run

            var match = Object.FindAnyObjectByType<GreyBoxMatch>();
            Assert.That(match, Is.Not.Null, "GreyBoxMatch failed to self-boot in an empty scene");

            var ball = GameObject.Find("Ball");
            Assert.That(ball, Is.Not.Null, "grey-box construction did not produce a Ball");
            Assert.That(GameObject.Find("Net"), Is.Not.Null);
            Assert.That(GameObject.Find("Home_1"), Is.Not.Null, "capsules missing");

            Vector3 p0 = ball.transform.position;
            float maxDelta = 0f;
            // ~4 s of sim: PreServe (1 s) + AI serve decision (0.5 s) + serve flight (~1.3 s) + receive.
            for (int i = 0; i < 240; i++)
            {
                yield return new WaitForFixedUpdate();
                maxDelta = Mathf.Max(maxDelta, (ball.transform.position - p0).magnitude);
            }

            Assert.That(maxDelta, Is.GreaterThan(1f),
                "ball never left the serve position — the tick sim is not driving the view");
        }
    }
}
