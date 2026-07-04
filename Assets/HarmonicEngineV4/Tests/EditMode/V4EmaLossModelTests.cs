using HarmonicEngineV4.Core;
using NUnit.Framework;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4EmaLossModelTests
    {
        [Test]
        public void StartsAtZero()
        {
            var model = new V4EmaLossModel(2, 0.5f);
            Assert.AreEqual(0f, model.TotalExpectedLoss);
        }

        [Test]
        public void ConvergesTowardConstantRate()
        {
            var model = new V4EmaLossModel(1, 0.2f);
            for (int i = 0; i < 100; i++)
            {
                model.Update(new[] { 10 });
            }

            Assert.AreEqual(10f, model.ExpectedLoss(0), 0.01f);
        }

        [Test]
        public void SingleStep_MatchesLerpFormula()
        {
            // expectedLoss = lerp(prev, actual, smoothing)
            Assert.AreEqual(2f, V4EmaLossModel.Step(0f, 10f > 0 ? 10 : 0, 0.2f), 1e-5f);
            Assert.AreEqual(4.4f, V4EmaLossModel.Step(3f, 10, 0.2f), 1e-5f);
        }

        [Test]
        public void DecaysWhenEjectionsStop()
        {
            var model = new V4EmaLossModel(1, 0.5f);
            model.Update(new[] { 8 });
            Assert.AreEqual(4f, model.ExpectedLoss(0), 1e-5f);
            model.Update(new[] { 0 });
            Assert.AreEqual(2f, model.ExpectedLoss(0), 1e-5f);
        }

        [Test]
        public void TotalSumsAcrossHoles()
        {
            var model = new V4EmaLossModel(3, 1f);
            model.Update(new[] { 1, 2, 3 });
            Assert.AreEqual(6f, model.TotalExpectedLoss, 1e-5f);
        }

        [Test]
        public void SmoothingIsClamped01()
        {
            var model = new V4EmaLossModel(1, 5f);
            model.Update(new[] { 4 });
            Assert.AreEqual(4f, model.ExpectedLoss(0), 1e-5f);
        }
    }
}
