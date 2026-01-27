#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Logistics;
using PureDOTS.Runtime.Logistics.Components;
using Unity.Entities;

namespace PureDOTS.Tests.Logistics
{
    public class ResourceReservationServiceTests
    {
        [Test]
        public void ReleaseReservation_SetsStatusToReleased()
        {
            var reservation = ResourceReservationService.ReserveInventory(
                Entity.Null,
                Entity.Null,
                "ore",
                10f,
                Entity.Null,
                currentTick: 1,
                ttlTicks: 30,
                reservationId: 7);

            ResourceReservationService.ReleaseReservation(ref reservation);

            Assert.AreEqual(ReservationStatus.Released, reservation.Status);
        }

        [Test]
        public void CheckReservationValidity_ReturnsFalseWhenExpired()
        {
            var reservation = ResourceReservationService.ReserveService(
                Entity.Null,
                ServiceType.Load,
                Entity.Null,
                slotTime: 10,
                currentTick: 10,
                ttlTicks: 5,
                reservationId: 1);

            var stillValid = ResourceReservationService.CheckReservationValidity(reservation, currentTick: 12);
            Assert.IsTrue(stillValid);

            var expired = ResourceReservationService.CheckReservationValidity(reservation, currentTick: 20);
            Assert.IsFalse(expired);
        }
    }
}
#endif

