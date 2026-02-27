using System;
using System.Threading.Tasks;
using ConcurrentBooking.Application.Dtos;
using ConcurrentBooking.Application.Repositories;
using ConcurrentBooking.Domain.Entities;

namespace ConcurrentBooking.Application.UseCases
{
    public class HoldSlotHandler
    {
        private readonly ISlotRepository _slotRepo;
        private readonly IHoldRepository _holdRepo;
        private readonly IBookingRepository _bookingRepo;

        public HoldSlotHandler(ISlotRepository slotRepo, IHoldRepository holdRepo, IBookingRepository bookingRepo)
        {
            _slotRepo = slotRepo;
            _holdRepo = holdRepo;
            _bookingRepo = bookingRepo;
        }

        public async Task<HoldSlotResult> Handle(HoldSlotRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var slot = await _slotRepo.GetByIdAsync(request.SlotId);
            if (slot == null) throw new InvalidOperationException("Slot not found");

            // Quick availability checks before attempting the DB insert
            if (await _bookingRepo.ExistsBookingForSlotAsync(slot.Id))
                throw new InvalidOperationException("Slot already booked");

            if (await _holdRepo.ExistsActiveHoldForSlotAsync(slot.Id))
                throw new InvalidOperationException("Slot already held");

            // RequestId stored for the DB-level unique constraint (extra safety net)
            var hold = new Hold(slot.Id, request.CustomerId, TimeSpan.FromSeconds(30), request.IdempotencyKey);
            await _holdRepo.AddAsync(hold);

            return new HoldSlotResult(hold.Id, hold.ExpiresAt);
        }
    }
}
