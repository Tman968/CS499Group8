import express from 'express';
import cors from 'cors';
import { pool } from './lib/database.js';
import authRouter from './routes/auth.js';
import session from 'express-session';
import connectPgSimple from 'connect-pg-simple';

const app = express();
const PORT = 3001;

// Allow requests from our React website
app.use(cors({
    origin: 'http://localhost:5173',
    credentials: true
}));

// Allow the server to read JSON request bodies
app.use(express.json());


const PgSession = connectPgSimple(session);

if (!process.env.SESSION_SECRET ||
    process.env.SESSION_SECRET.length < 32) {
    throw new Error('A SESSION_SECRET of at least 32 characters is required');
}

if (process.env.NODE_ENV === 'production') {
    app.set('trust proxy', 1);
}

app.use(session({
    store: new PgSession({
        pool,
        schemaName: 'jumpcraft',
        tableName: 'user_sessions'
    }),

    name: 'jumpcraft.sid',
    secret: process.env.SESSION_SECRET,

    resave: false,
    saveUninitialized: false,

    cookie: {
        httpOnly: true,
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'lax',
        maxAge: 24 * 60 * 60 * 1000
    }
}));


// Creates first authentication route for registering and logging in users
app.use('/api/auth', authRouter);

// Test API endpoint
app.get('/api/health', (req, res) => {
    res.json({
        status: 'success',
        message: 'JumpCraft backend is running!'
    });
});

app.get('/api/db-health', async (req, res) => {
    try {
        const result = await pool.query(
            'SELECT COUNT(*) AS count FROM jumpcraft.player_accounts'
        );

        res.json({
            status: 'success',
            message: 'PostgreSQL connected!',
            accounts: Number(result.rows[0].count)
        });

    } catch (error) {
        console.error('Database error:', error.message);

        res.status(500).json({
            status: 'error',
            message: 'Database connection failed'
        });
    }
});

// Start server
app.listen(PORT, () => {
    console.log(`JumpCraft server running at http://localhost:${PORT}`);
});
