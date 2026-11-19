import 'dotenv/config';
import pg from 'pg';

const { Pool } = pg;

if (!process.env.DATABASE_URL) {
    throw new Error('DATABASE_URL is missing');
}

export const pool = new Pool({
    connectionString: process.env.DATABASE_URL,
    max: 3,
    connectionTimeoutMillis: 10000
});

pool.on('error', (error) => {
    console.error('Database pool error:', error.message);
});