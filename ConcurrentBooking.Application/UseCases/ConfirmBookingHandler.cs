using System;
using System.Threading.Tasks;
using ConcurrentBooking.Application.Dtos;
using ConcurrentBooking.Application.Repositories;
using ConcurrentBooking.Domain.Entities;

namespace ConcurrentBooking.Application.UseCases
{
    public class ConfirmBookingHandler
    {
        private readonly IHoldRepository _holdRepo;
        private readonly IBookingRepository _bookingRepo;

        public ConfirmBookingHandler(IHoldRepository holdRepo, IBookingRepository bookingRepo)
        {
            _holdRepo = holdRepo;
            _bookingRepo = bookingRepo;
        }

        public async Task<ConfirmBookingResult> Handle(ConfirmBookingRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var hold = await _holdRepo.GetByIdAsync(request.HoldId)
                ?? throw new InvalidOperationException("Hold not found");

            if (hold.IsExpired()) throw new InvalidOperationException("Hold expired");
            if (hold.Status != HoldStatus.Active) throw new InvalidOperationException("Hold not active");

            // Quick check before the DB insert (final guarantee is the UNIQUE constraint on bookings.SlotId)
            if (await _bookingRepo.ExistsBookingForSlotAsync(hold.SlotId))
                throw new InvalidOperationException("Slot already booked");

            // RequestId stored for the DB-level unique constraint (extra safety net)
            var booking = new Booking(hold.SlotId, request.CustomerId, hold.Id, request.IdempotencyKey);
            await _bookingRepo.AddAsync(booking);

            hold.MarkConsumed();
            await _holdRepo.SaveChangesAsync();

            return new ConfirmBookingResult(booking.Id);
        }
    }
}
