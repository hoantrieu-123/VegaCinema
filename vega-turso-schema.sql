CREATE TABLE "AspNetRoles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetRoles" PRIMARY KEY,
    "Name" TEXT NULL,
    "NormalizedName" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL
);


CREATE TABLE "CinemaClusters" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_CinemaClusters" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "City" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL
);


CREATE TABLE "Movies" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Movies" PRIMARY KEY AUTOINCREMENT,
    "Title" TEXT NOT NULL,
    "Genre" TEXT NOT NULL,
    "DurationMinutes" INTEGER NOT NULL,
    "Description" TEXT NOT NULL,
    "PosterUrl" TEXT NOT NULL,
    "AgeRating" TEXT NOT NULL,
    "ReleaseDate" TEXT NOT NULL,
    "Status" TEXT NOT NULL
);


CREATE TABLE "AspNetRoleClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY AUTOINCREMENT,
    "RoleId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "AspNetUsers" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetUsers" PRIMARY KEY,
    "FullName" TEXT NOT NULL,
    "BirthYear" INTEGER NULL,
    "Gender" INTEGER NULL,
    "Address" TEXT NULL,
    "CinemaClusterId" INTEGER NULL,
    "IsActive" INTEGER NOT NULL,
    "UserName" TEXT NULL,
    "NormalizedUserName" TEXT NULL,
    "Email" TEXT NULL,
    "NormalizedEmail" TEXT NULL,
    "EmailConfirmed" INTEGER NOT NULL,
    "PasswordHash" TEXT NULL,
    "SecurityStamp" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL,
    "PhoneNumber" TEXT NULL,
    "PhoneNumberConfirmed" INTEGER NOT NULL,
    "TwoFactorEnabled" INTEGER NOT NULL,
    "LockoutEnd" TEXT NULL,
    "LockoutEnabled" INTEGER NOT NULL,
    "AccessFailedCount" INTEGER NOT NULL,
    CONSTRAINT "FK_AspNetUsers_CinemaClusters_CinemaClusterId" FOREIGN KEY ("CinemaClusterId") REFERENCES "CinemaClusters" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "Cinemas" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Cinemas" PRIMARY KEY AUTOINCREMENT,
    "CinemaClusterId" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "Address" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL,
    CONSTRAINT "FK_Cinemas_CinemaClusters_CinemaClusterId" FOREIGN KEY ("CinemaClusterId") REFERENCES "CinemaClusters" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "AspNetUserClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "AspNetUserLogins" (
    "LoginProvider" TEXT NOT NULL,
    "ProviderKey" TEXT NOT NULL,
    "ProviderDisplayName" TEXT NULL,
    "UserId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
    CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "AspNetUserRoles" (
    "UserId" TEXT NOT NULL,
    "RoleId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "AspNetUserTokens" (
    "UserId" TEXT NOT NULL,
    "LoginProvider" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Value" TEXT NULL,
    CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
    CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "ScreeningRooms" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ScreeningRooms" PRIMARY KEY AUTOINCREMENT,
    "CinemaId" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "RowCount" INTEGER NOT NULL,
    "ColumnCount" INTEGER NOT NULL,
    "IsActive" INTEGER NOT NULL,
    CONSTRAINT "FK_ScreeningRooms_Cinemas_CinemaId" FOREIGN KEY ("CinemaId") REFERENCES "Cinemas" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "Seats" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Seats" PRIMARY KEY AUTOINCREMENT,
    "ScreeningRoomId" INTEGER NOT NULL,
    "Row" TEXT NOT NULL,
    "Number" INTEGER NOT NULL,
    "Type" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL,
    CONSTRAINT "FK_Seats_ScreeningRooms_ScreeningRoomId" FOREIGN KEY ("ScreeningRoomId") REFERENCES "ScreeningRooms" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "Showtimes" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Showtimes" PRIMARY KEY AUTOINCREMENT,
    "MovieId" INTEGER NOT NULL,
    "ScreeningRoomId" INTEGER NOT NULL,
    "StartsAtUtc" TEXT NOT NULL,
    "EndsAtUtc" TEXT NOT NULL,
    "StandardPrice" TEXT NOT NULL,
    "VipPrice" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL,
    CONSTRAINT "FK_Showtimes_Movies_MovieId" FOREIGN KEY ("MovieId") REFERENCES "Movies" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Showtimes_ScreeningRooms_ScreeningRoomId" FOREIGN KEY ("ScreeningRoomId") REFERENCES "ScreeningRooms" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "Bookings" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Bookings" PRIMARY KEY,
    "Code" TEXT NOT NULL,
    "UserId" TEXT NOT NULL,
    "ShowtimeId" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "ExpiresAtUtc" TEXT NOT NULL,
    "Total" TEXT NOT NULL,
    CONSTRAINT "FK_Bookings_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Bookings_Showtimes_ShowtimeId" FOREIGN KEY ("ShowtimeId") REFERENCES "Showtimes" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "Payments" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Payments" PRIMARY KEY,
    "BookingId" TEXT NOT NULL,
    "TransactionCode" TEXT NOT NULL,
    "Amount" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "ConfirmedAtUtc" TEXT NULL,
    "GatewayReceiptJson" TEXT NULL,
    "ReceiptReceivedAtUtc" TEXT NULL,
    CONSTRAINT "FK_Payments_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "ShowtimeSeats" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ShowtimeSeats" PRIMARY KEY AUTOINCREMENT,
    "ShowtimeId" INTEGER NOT NULL,
    "SeatId" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "HoldBookingId" TEXT NULL,
    "HoldExpiresAtUtc" TEXT NULL,
    "Price" TEXT NOT NULL,
    CONSTRAINT "FK_ShowtimeSeats_Bookings_HoldBookingId" FOREIGN KEY ("HoldBookingId") REFERENCES "Bookings" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ShowtimeSeats_Seats_SeatId" FOREIGN KEY ("SeatId") REFERENCES "Seats" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ShowtimeSeats_Showtimes_ShowtimeId" FOREIGN KEY ("ShowtimeId") REFERENCES "Showtimes" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "TransactionLogs" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_TransactionLogs" PRIMARY KEY AUTOINCREMENT,
    "BookingId" TEXT NULL,
    "Event" TEXT NOT NULL,
    "Message" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    CONSTRAINT "FK_TransactionLogs_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "BookingDetails" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_BookingDetails" PRIMARY KEY AUTOINCREMENT,
    "BookingId" TEXT NOT NULL,
    "ShowtimeSeatId" INTEGER NOT NULL,
    "Price" TEXT NOT NULL,
    CONSTRAINT "FK_BookingDetails_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_BookingDetails_ShowtimeSeats_ShowtimeSeatId" FOREIGN KEY ("ShowtimeSeatId") REFERENCES "ShowtimeSeats" ("Id") ON DELETE RESTRICT
);


CREATE TABLE "Tickets" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Tickets" PRIMARY KEY AUTOINCREMENT,
    "Code" TEXT NOT NULL,
    "BookingDetailId" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "IssuedAtUtc" TEXT NOT NULL,
    "UsedAtUtc" TEXT NULL,
    CONSTRAINT "FK_Tickets_BookingDetails_BookingDetailId" FOREIGN KEY ("BookingDetailId") REFERENCES "BookingDetails" ("Id") ON DELETE RESTRICT
);


CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");


CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");


CREATE INDEX "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");


CREATE INDEX "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");


CREATE INDEX "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");


CREATE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");


CREATE INDEX "IX_AspNetUsers_CinemaClusterId" ON "AspNetUsers" ("CinemaClusterId");


CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");


CREATE UNIQUE INDEX "IX_BookingDetails_BookingId_ShowtimeSeatId" ON "BookingDetails" ("BookingId", "ShowtimeSeatId");


CREATE INDEX "IX_BookingDetails_ShowtimeSeatId" ON "BookingDetails" ("ShowtimeSeatId");


CREATE UNIQUE INDEX "IX_Bookings_Code" ON "Bookings" ("Code");


CREATE INDEX "IX_Bookings_ShowtimeId" ON "Bookings" ("ShowtimeId");


CREATE INDEX "IX_Bookings_Status_ExpiresAtUtc" ON "Bookings" ("Status", "ExpiresAtUtc");


CREATE INDEX "IX_Bookings_UserId_CreatedAtUtc" ON "Bookings" ("UserId", "CreatedAtUtc");


CREATE INDEX "IX_Cinemas_CinemaClusterId" ON "Cinemas" ("CinemaClusterId");


CREATE INDEX "IX_Payments_BookingId" ON "Payments" ("BookingId");


CREATE UNIQUE INDEX "IX_Payments_TransactionCode" ON "Payments" ("TransactionCode");


CREATE INDEX "IX_ScreeningRooms_CinemaId" ON "ScreeningRooms" ("CinemaId");


CREATE UNIQUE INDEX "IX_Seats_ScreeningRoomId_Row_Number" ON "Seats" ("ScreeningRoomId", "Row", "Number");


CREATE INDEX "IX_Showtimes_MovieId_StartsAtUtc" ON "Showtimes" ("MovieId", "StartsAtUtc");


CREATE INDEX "IX_Showtimes_ScreeningRoomId_StartsAtUtc_EndsAtUtc" ON "Showtimes" ("ScreeningRoomId", "StartsAtUtc", "EndsAtUtc");


CREATE INDEX "IX_ShowtimeSeats_HoldBookingId_Status" ON "ShowtimeSeats" ("HoldBookingId", "Status");


CREATE INDEX "IX_ShowtimeSeats_SeatId" ON "ShowtimeSeats" ("SeatId");


CREATE UNIQUE INDEX "IX_ShowtimeSeats_ShowtimeId_SeatId" ON "ShowtimeSeats" ("ShowtimeId", "SeatId");


CREATE UNIQUE INDEX "IX_Tickets_BookingDetailId" ON "Tickets" ("BookingDetailId");


CREATE UNIQUE INDEX "IX_Tickets_Code" ON "Tickets" ("Code");


CREATE INDEX "IX_TransactionLogs_BookingId" ON "TransactionLogs" ("BookingId");


CREATE INDEX "IX_TransactionLogs_CreatedAtUtc" ON "TransactionLogs" ("CreatedAtUtc");


